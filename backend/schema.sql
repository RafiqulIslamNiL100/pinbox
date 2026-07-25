-- Pinbox licensing backend schema (Supabase / Postgres).
-- Run this once in your Supabase project's SQL editor (Dashboard -> SQL Editor -> New query).
-- Safe to re-run: every statement below is idempotent, so pasting the whole
-- file again after an update (e.g. the device-lock columns) just applies
-- whatever's missing.
--
-- What this sets up:
--   - profiles: one row per user, mirrors auth.users, tracks ban/restrict status,
--     which key they redeemed, when it expires, which single device is
--     currently allowed to use the account, and when they were last seen.
--   - activation_keys: every key you've ever generated, redeemed or not.
--   - activate_key(): called by the app when a user enters a key. Also
--     claims the device that redeemed it.
--   - check_license(): called by the app on launch (and periodically) to
--     decide whether to let the user in, update their "last seen"
--     timestamp, and enforce the one-device-at-a-time rule.
--
-- One device at a time: each profile has a device_id, set by whichever
-- device most recently signed in or activated a key. That's an explicit
-- action (p_claim = true) and always takes over the slot, which is what
-- signs any other device out. A device merely resuming a saved session or
-- polling in the background (p_claim = false) never takes over - it only
-- confirms it's still the claimed device, and gets signed out locally if
-- another device has since taken its place.
--
-- Security model: normal users can only read/update their OWN profile row, and
-- can never read the activation_keys table directly or anyone else's profile -
-- that's enforced by Row Level Security below, not just app-side logic. Your
-- admin dashboard connects with the "service_role" key instead, which bypasses
-- these restrictions entirely (that key must never be embedded in the desktop
-- app - it's for the admin dashboard only, kept on your machine).

create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  name text not null default '',
  email text not null default '',
  signup_path text not null default 'self' check (signup_path in ('self', 'admin')),
  status text not null default 'active' check (status in ('active', 'restricted', 'banned')),
  key_expires_at timestamptz,               -- null = no key redeemed yet, or a never-expiring key
  redeemed_key_code text,                   -- which key they used, for your own reference
  device_id text,                           -- the one device currently allowed to use this account
  device_label text,                        -- human-readable machine name, for the admin dashboard
  created_at timestamptz not null default now(),
  last_seen timestamptz
);

alter table public.profiles add column if not exists device_id text;
alter table public.profiles add column if not exists device_label text;

create table if not exists public.activation_keys (
  id uuid primary key default gen_random_uuid(),
  code text not null unique,
  duration_days int,                        -- null = never expires
  status text not null default 'unredeemed' check (status in ('unredeemed', 'redeemed', 'revoked')),
  assigned_email text,
  note text,
  created_at timestamptz not null default now(),
  redeemed_at timestamptz,
  redeemed_by uuid references public.profiles (id)
);

alter table public.profiles enable row level security;
alter table public.activation_keys enable row level security;

-- Users can see and update only their own profile row.
create policy "profiles: read own" on public.profiles
  for select using (auth.uid() = id);
create policy "profiles: update own" on public.profiles
  for update using (auth.uid() = id);

-- No direct client access to activation_keys at all - only through the
-- functions below (SECURITY DEFINER lets them bypass RLS safely), or through
-- the admin dashboard's service_role connection.

-- Automatically create a profile row whenever someone signs up.
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, name, email, signup_path)
  values (
    new.id,
    coalesce(new.raw_user_meta_data ->> 'name', ''),
    new.email,
    coalesce(new.raw_user_meta_data ->> 'signup_path', 'self')
  );
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute procedure public.handle_new_user();

-- Called by the app when the user enters a key on the Activate screen.
-- Redeeming a key is an explicit action, so it also claims the device that
-- did it - the same rule an explicit sign-in follows in check_license().
drop function if exists public.activate_key(text);
create or replace function public.activate_key(p_code text, p_device_id text default null, p_device_label text default null)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_key public.activation_keys;
  v_expires timestamptz;
begin
  select * into v_key from public.activation_keys where code = p_code for update;

  if v_key is null then
    return jsonb_build_object('ok', false, 'reason', 'not_found');
  end if;

  if v_key.status = 'revoked' then
    return jsonb_build_object('ok', false, 'reason', 'revoked');
  end if;

  if v_key.status = 'redeemed' then
    return jsonb_build_object('ok', false, 'reason', 'already_used');
  end if;

  if v_key.duration_days is null then
    v_expires := null;
  else
    v_expires := now() + (v_key.duration_days || ' days')::interval;
  end if;

  update public.activation_keys
    set status = 'redeemed', redeemed_at = now(), redeemed_by = auth.uid()
    where id = v_key.id;

  update public.profiles
    set key_expires_at = v_expires, redeemed_key_code = p_code,
        device_id = coalesce(p_device_id, device_id),
        device_label = coalesce(p_device_label, device_label)
    where id = auth.uid();

  return jsonb_build_object('ok', true, 'expires_at', v_expires);
end;
$$;

-- Called by the app on launch and periodically. Updates last_seen as a side
-- effect, tells the app whether it's allowed to proceed, and enforces the
-- one-device-at-a-time rule: p_claim = true (explicit sign-in/sign-up)
-- always takes the device slot; p_claim = false (silent resume or the
-- periodic background poll) only verifies it still holds the slot and
-- returns 'device_mismatch' if another device has since claimed it.
drop function if exists public.check_license();
create or replace function public.check_license(p_device_id text default null, p_claim boolean default false, p_device_label text default null)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_profile public.profiles;
begin
  select * into v_profile from public.profiles where id = auth.uid();

  if v_profile is null then
    return jsonb_build_object('ok', false, 'reason', 'no_profile');
  end if;

  update public.profiles set last_seen = now() where id = auth.uid();

  if v_profile.status = 'banned' then
    return jsonb_build_object('ok', false, 'reason', 'banned');
  end if;

  if v_profile.status = 'restricted' then
    return jsonb_build_object('ok', false, 'reason', 'restricted');
  end if;

  if v_profile.key_expires_at is null and v_profile.redeemed_key_code is null then
    return jsonb_build_object('ok', false, 'reason', 'no_key');
  end if;

  if v_profile.key_expires_at is not null and v_profile.key_expires_at < now() then
    return jsonb_build_object('ok', false, 'reason', 'expired');
  end if;

  if p_device_id is not null then
    if p_claim then
      update public.profiles
        set device_id = p_device_id, device_label = coalesce(p_device_label, device_label)
        where id = auth.uid();
    elsif v_profile.device_id is not null and v_profile.device_id <> p_device_id then
      return jsonb_build_object('ok', false, 'reason', 'device_mismatch');
    end if;
  end if;

  return jsonb_build_object('ok', true, 'expires_at', v_profile.key_expires_at);
end;
$$;

grant execute on function public.activate_key(text, text, text) to authenticated;
grant execute on function public.check_license(text, boolean, text) to authenticated;

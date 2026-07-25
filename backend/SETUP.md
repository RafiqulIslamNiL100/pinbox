# Setting up the Pinbox backend (Supabase)

This is the one part of the whole system that has to be you — it's a
signup on an external service, which I can't do on your behalf. It takes
about 5 minutes.

## 1. Create your project

1. Go to [supabase.com](https://supabase.com) and sign up (free, no credit
   card required for the free tier).
2. Click **New project**. Pick any name (e.g. "pinbox"), set a database
   password (save it somewhere), and choose the region closest to you.
3. Wait ~2 minutes for it to finish provisioning.

## 2. Turn off email confirmation

By design, signing up in Pinbox should work immediately with no email
verification step — the activation key is the real gate, not the email.

- In your Supabase project: **Authentication** → **Providers** → **Email**
- Turn **OFF** "Confirm email"
- Save

## 3. Run the schema

- In your Supabase project: **SQL Editor** → **New query**
- Open `backend/schema.sql` from this repo, paste the whole thing in, and
  click **Run**.
- You should see "Success. No rows returned." — that's correct, it just
  created the tables and functions.
- The whole file is safe to paste and run again any time it's updated
  (e.g. after pulling a newer version of the app) - every statement only
  creates or alters what's missing, it won't touch your existing data.

## 4. Get your keys

- **Project Settings** → **API**
- Copy the **Project URL** (looks like `https://xxxxx.supabase.co`)
- Copy the **anon / public** key (a long string starting with `eyJ...`)
- Copy the **service_role** key too (also starts with `eyJ...`, but marked
  "secret" — **never put this one in the desktop app**, only the admin
  dashboard uses it, and it should never leave your own machine)

## 5. Point the app at your project

Open `src/Pinbox/supabase-config.json` in the repo and replace the
placeholders with your real values:

```json
{
  "url": "https://xxxxx.supabase.co",
  "anonKey": "eyJ...your anon key..."
}
```

Only the **Project URL** and **anon key** go here — never the service_role
key. Rebuild the app (or just replace this file next to `Pinbox.exe` in an
existing install) and it's live.

## 6. Open the admin dashboard

Open `backend/admin-dashboard.html` in any browser (just double-click it —
no server, no install). Paste in your **Project URL** and **service_role**
key when it asks. From there you can see every user, ban/restrict/delete
accounts, and generate activation keys with a custom expiry to hand out to
customers. Keep this file and your service_role key on your own machine only.

## What this buys you

- Every signup automatically gets a row in your database (no code needed —
  a trigger handles it).
- The app calls `activate_key` when someone enters a key, and
  `check_license` on launch (and periodically) to enforce expiry, bans, and
  restrictions, and to record when they were last seen.
- You never touch SQL again after this — the admin dashboard manages
  everything through a normal web UI.

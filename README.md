# SGY Check-In

An app for Friday-night youth group check-in. Replaces the paper tick-list and new-member
form: search for a returning youth by name and check them in with one tap, or register a new
youth on the spot.

It runs in Docker on the church's network — no internet needed once it's set up.

## Two access levels

There are two passwords, one for each level:

- **Volunteer** — can check youth in, register new ones, and correct a youth's details from
  the check-in log. This is what most helpers use.
- **Admin** — can do everything a volunteer can, plus manage all youth profiles (search,
  edit details, archive/restore), set the leaders' notes on a profile, and see the
  dashboard. Use the **Profiles** and **Dashboard** pages in the top menu.

Everyone types their password on the same screen; whichever password they enter decides what
they can do.

## Leaders' notes on a profile

Every youth profile has a **Leaders' notes** section, near the bottom of their details:

- **Status** — everyone starts on *No concerns*. An admin can change this to *Trouble maker*.
- **Care Village** — a separate tick box, so a youth can be both a trouble maker and in
  Care Village at the same time.
- **Comment** — free text for anything leaders should know: allergies, pastoral notes, why
  someone is flagged.

**Only admins can change these.** Volunteers can see them but the fields are read-only for
them — useful at the door, since a flagged youth shows a small coloured dot next to their
name in the check-in search: **red** for trouble maker, **amber** for Care Village. The dot
says nothing about why; the reason lives on the profile.

Keep in mind these notes are about children. Anyone with the volunteer password can read
them, and the app does not record who changed a note or when.

## Dashboard (admins only)

The **Dashboard** page answers "how are we doing?" for any stretch of time. Pick a date
range — or use the *Last 6 weeks* / *12 weeks* / *year* buttons — and it shows:

- how many checked in today, how many different youth came in the period, the average per
  evening, and how many newly registered
- attendance for each evening, and a breakdown by grade
- **Came most often** — the youth who showed up the most, out of the evenings you held
- **Haven't come in this period** — youth still on the books who didn't come, with when they
  were last seen (or "Never checked in"), longest absences first. Click a name to open their
  profile.
- **Export CSV** — one row per arrival for the chosen range, for the church office. It
  opens/downloads straight from the browser.

## Setting it up

**1. Set the two passwords.** The app won't let anyone in until you do this.

Generate a code for each password:

```
docker compose run --rm sgy-checkin --hash-password "YourVolunteerPassword"
docker compose run --rm sgy-checkin --hash-password "YourAdminPassword"
```

Each prints a long code. Open `docker-compose.yml` and paste them in:

- the volunteer code as `CheckInAuth__VolunteerPasswordHash`
- the admin code as `CheckInAuth__AdminPasswordHash`

(You can leave one blank to disable that level — but you'll usually want both set.)

**2. Check the time zone.** `docker-compose.yml` has `TZ: Africa/Johannesburg` — change this
if the church isn't in South Africa. This just makes sure check-in times and "today" are
correct.

**3. Start the app.**

```
docker compose up -d --build
```

Now open `http://<the computer's IP address>:8080` on any phone, tablet, or laptop on the
church WiFi. You can find the computer's IP address by running `ipconfig` and looking for the
IPv4 address.

## Everyday use

- **To stop it:** `docker compose down` — your data is safe, it's not stored inside the
  container.
- **To start it again:** `docker compose up -d` (no need for `--build` unless something in the
  app changed).
- **To update after a change:** `docker compose up -d --build`.
- **To change a password:** repeat step 1 for that level with a new password, paste in the new
  code, then `docker compose up -d`.

## Where the data lives

Everything (youth details and check-ins) is stored in a Docker volume called
`sgy_checkin_data`. It survives stopping, starting, and rebuilding the app. If you want
backups, ask whoever manages the church's Docker setup to copy that volume somewhere safe now
and then.

## Running it on your own computer to test changes

Needs the .NET 10 SDK installed.

```
dotnet ef database update --project src/SGY.CheckIn
dotnet run --project src/SGY.CheckIn
```

Then open the address it prints. Local testing passwords: **checkin123** (volunteer) and
**admin123** (admin).

# SGY Check-In

An app for Friday-night youth group check-in. Replaces the paper tick-list and new-member
form: search for a returning youth by name and check them in with one tap, or register a new
youth on the spot (they get checked in automatically).

It runs in Docker on the church's network — no internet needed once it's set up.

## Setting it up

**1. Set the staff password.** The app won't let anyone in until you do this.

```
docker compose run --rm sgy-checkin --hash-password "YourChosenPassword"
```

This prints a long code. Copy it, open `docker-compose.yml`, and paste it in as the value of
`CheckInAuth__PasswordHash`.

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
- **To change the password:** repeat step 1 above with a new password, paste in the new code,
  then `docker compose up -d`.

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

Then open the address it prints. The password for local testing is **checkin123**.

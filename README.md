# SGY Check-In

An app for Friday-night youth check-in. It replaces the paper tick-list and the new-member
form.

Search for a youth by name and check them in with one tap. If they are new, register them on
the spot.

The app runs on one computer at the church. Everyone else uses it from their phone, tablet or
laptop on the church WiFi. It does not need the internet once it is set up.

## Who can do what

There are two passwords. The password someone types decides what they can do.

**Volunteer** (most helpers use this one):

- check youth in
- register new youth
- fix a youth's details from the check-in log

**Admin** (youth leaders):

- everything a volunteer can do
- manage all profiles on the **Profiles** page: search, edit, archive and restore
- set the leaders' notes on a profile
- see the **Dashboard**

Everyone signs in on the same screen.

## Leaders' notes on a profile

Near the bottom of each youth's details there is a **Leaders' notes** section:

- **Status.** Everyone starts on *No concerns*. An admin can change this to *Trouble maker*.
- **Care Village.** A separate tick box. A youth can be a trouble maker and in Care Village
  at the same time.
- **Comment.** Free text for anything leaders should know, such as allergies, pastoral notes,
  or why someone is flagged.

Only admins can change these. Volunteers can read them but cannot edit them.

A flagged youth shows a small coloured dot next to their name in the check-in search. Red
means trouble maker. Amber means Care Village. The dot does not say why. The reason is on the
profile.

Please remember these notes are about children. Anyone with the volunteer password can read
them. The app does not record who changed a note, or when.

## Dashboard (admins only)

The **Dashboard** page shows how the group is doing over any stretch of time. Pick a date
range, or use the *Last 6 weeks*, *Last 12 weeks* and *Last year* buttons.

It shows:

- how many checked in today
- how many different youth came in that time
- the average number per evening
- how many new youth registered
- attendance for each evening, and a breakdown by grade
- **Came most often.** The youth who showed up the most, out of the evenings you held.
- **Haven't come in this period.** Youth still on the books who did not come. It shows when
  each was last seen, or "Never checked in". The longest absences are first. Click a name to
  open their profile.
- **Export CSV.** A spreadsheet file with one line per arrival, for the church office.

## Setting it up

You need Docker on the computer that will run the app. Docker is free software that runs the
app in its own little box, so you do not have to install anything else.

**1. Set the two passwords.** Nobody can sign in until you do this.

Run these two commands, using your own passwords:

```
docker compose run --rm sgy-checkin --hash-password "YourVolunteerPassword"
docker compose run --rm sgy-checkin --hash-password "YourAdminPassword"
```

Each command prints a long code. Open the file `docker-compose.yml` and paste the codes in:

- the volunteer code goes after `CheckInAuth__VolunteerPasswordHash:`
- the admin code goes after `CheckInAuth__AdminPasswordHash:`

Keep the quote marks around each code. You can leave one blank to switch that level off, but
you will usually want both.

**2. Check the time zone.** In `docker-compose.yml` you will see `TZ: Africa/Johannesburg`.
Change it only if your church is not in South Africa. This keeps check-in times and "today"
correct.

**3. Start the app.**

```
docker compose up -d --build
```

**4. Open it.** On any phone, tablet or laptop on the church WiFi, go to:

```
http://<the computer's address>:8080
```

To find the address, run `ipconfig` on that computer (or `ip addr` on Linux) and look for the
IPv4 address. It usually looks like `192.168.0.50`.

## Giving it an easy web address

Typing an address like `192.168.0.50:8080` is easy to get wrong. Whoever looks after the
church network can set up a friendly name such as `checkin` instead. The install guide written
for them explains how.

## Everyday use

- **Stop it:** `docker compose down`. Your data is safe. It is not kept inside the app.
- **Start it again:** `docker compose up -d`
- **Update it after a change:** `docker compose up -d --build`
- **Change a password:** do step 1 again with the new password, paste in the new code, then
  run `docker compose up -d`.

## Where the data lives

All the youth details and check-ins are kept in a storage area Docker looks after, called
`sgy_checkin_data`. It stays put when you stop, start or update the app.

For backups, ask whoever looks after Docker at the church to copy that storage area somewhere
safe now and then.

## Running it on your own computer to make changes

You need the .NET 10 SDK.

```
dotnet run --project src/SGY.CheckIn
```

Open the address it prints. The app sets up its own database the first time it runs.

Test passwords for your own computer only: **checkin123** (volunteer) and **admin123**
(admin).

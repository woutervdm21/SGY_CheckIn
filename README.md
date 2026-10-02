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

- check youth in, after confirming their details and their parent's details with them
- undo a check-in made by mistake
- register new youth
- fix a youth's details from the check-in screen or the check-in log
- set the Care Village tick and the comment in the leaders' notes

**Admin** (youth leaders):

- everything a volunteer can do
- manage all profiles on the **Profiles** page: search, edit, archive and restore
- set a youth's status to *Trouble maker*
- see the **Dashboard**
- change the volunteer password on the **Settings** page

Everyone signs in on the same screen. First choose a group: **Kids**, **Youth** or **Young
Adults**. Only Youth is built so far. Kids and Young Adults show "Under development" for now.

## Leaders' notes on a profile

Near the bottom of each youth's details there is a **Leaders' notes** section:

- **Status.** Everyone starts on *No concerns*. An admin can change this to *Trouble maker*.
- **Care Village.** A separate tick box. A youth can be a trouble maker and in Care Village
  at the same time.
- **Comment.** Free text for anything leaders should know, such as allergies, pastoral notes,
  or anything else that helps on the night.

Anyone can tick Care Village and edit the comment. Only admins can change the status.
Volunteers can see the status but cannot change it.

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
- **Export data.** Opens the Export page (see below).

## Export data (admins only)

Open **Export data** from the Dashboard. There are two spreadsheet (CSV) downloads, each with
every detail on file. Choose what to include, check the number of rows it shows, then press
**Download CSV**.

- **Check-in history.** One line per arrival, with the youth's full details. Filter by dates,
  grade, and whether to include archived youth.
- **Youth list.** One line per youth, with their details, leaders' notes, total check-ins, and
  first and last check-in. Filter by active or archived, grade, and leaders' notes (trouble
  makers or Care Village).

Dates in the files are written as year-month-day (2026-10-02), so every spreadsheet reads them
the same way. The files include leaders' notes about children, so keep them private and delete
them when you are done.

## Setting it up

You need Docker on the computer that will run the app. Docker is free software that runs the
app in its own little box, so you do not have to install anything else.

**1. Set the admin password.** Nobody can sign in until you do this.

Run this command, using your own password:

```
docker compose run --rm sgy-checkin --hash-password "YourAdminPassword"
```

It prints a long code. Open the file `docker-compose.yml` and paste the code in after
`CheckInAuth__AdminPasswordHash:`. Keep the quote marks around it.

You set the volunteer password later, from inside the app (step 5).

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

**5. Set the volunteer password.** Sign in with the admin password, open **Settings**, and
type the volunteer password. Volunteers cannot sign in until you do this.

## Giving it an easy web address

Typing an address like `192.168.0.50:8080` is easy to get wrong. Whoever looks after the
church network can set up a friendly name such as `checkin` instead. The install guide written
for them explains how.

## Everyday use

- **Stop it:** `docker compose down`. Your data is safe. It is not kept inside the app.
- **Start it again:** `docker compose up -d`
- **Update it after a change:** `docker compose up -d --build`
- **Change the volunteer password:** sign in as an admin and open **Settings**. Every device
  signed in as a volunteer is signed out and needs the new password.
- **Change the admin password:** do step 1 again with the new password, paste in the new
  code, then run `docker compose up -d`.
- **Updating from an older version:** if your `docker-compose.yml` still has a
  `CheckInAuth__VolunteerPasswordHash` line, the app copies that volunteer password across
  the first time it starts. After that you can delete the line. Changes to it do nothing.

## Where the data lives

All the youth details and check-ins are kept in a storage area Docker looks after, called
`sgy_checkin_data`. It stays put when you stop, start or update the app.

## Backups

Once a week the app saves a full copy of the database by itself. It needs no setup. Each copy
has everything: every youth, every check-in since the app was first used, leaders' notes, and
settings including the volunteer password. The app keeps the last 12 copies, about three months.

Because church computers are often switched off at night, the app does not wait for a set time.
A few minutes after it starts, and then every hour while it runs, it checks the newest copy. If
that copy is a week old, it makes a new one. A Friday night is enough to keep it up to date.

The copies are saved on the same computer as the app, so they do not help if that computer's
disk fails. **Now and then, an admin should download one and keep it somewhere else**, such as a
USB stick or the church Drive:

1. Sign in as an admin and open **Settings**.
2. Under **Backups**, press **Back up now**.
3. Press **Download** next to the newest backup.

Settings also shows when the last backup was made, and warns you if it failed or is overdue.

To save the weekly copies straight into a folder that syncs to OneDrive or Google Drive, follow
the note about backups in `docker-compose.yml`.

### Restoring a backup

This puts every record back as it was when the backup was made. The app saves a copy of the
current data first, so a restore can itself be undone from the backup list.

1. Download the backup you want from **Settings** and put it in the same folder as
   `docker-compose.yml`. Rename it `restore.db`.
2. Stop the app: `docker compose stop`
3. Copy the file into the app's storage: `docker cp restore.db sgy-checkin:/data/restore.db`
4. Start the app: `docker compose start`

When the app starts, it notices `restore.db`, swaps it in, and deletes it. Sign in and check the
data is back, then delete your own `restore.db`.

## Running it on your own computer to make changes

You need the .NET 10 SDK.

```
dotnet run --project src/SGY.CheckIn
```

Open the address it prints. The app sets up its own database the first time it runs.

Test passwords for your own computer only: **checkin123** (volunteer) and **admin123**
(admin).

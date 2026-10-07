# SG Check-In

An app for checking people in at SolidGround's three groups: **Kids**, **Youth** and **Young
Adults**. It replaces the paper tick-list and the new-member form.

Search for someone by name and check them in with one tap. If they are new, register them on
the spot. Kids get a name label printed as they check in.

The app runs on one computer at the church. Everyone else uses it from their phone, tablet or
laptop on the church WiFi. It does not need the internet once it is set up.

## The three groups

Everyone signs in on the same screen. First choose a group: **Kids**, **Youth** or **Young
Adults**. After that the app only shows that group: its people, its check-ins, its dashboard
and its exports. The group's name is at the top left of every page.

Each group records slightly different details:

| | Kids | Youth | Young Adults |
|---|---|---|---|
| Name, surname, date of birth | Yes | Yes | Yes |
| Their own cell number | No | Yes | Yes |
| Grade | RRR, RR, R and 1–7 | 8–12 | No |
| Parent / guardian details | Yes | Yes | No |
| Medical & allergies (printed on their label) | Yes | No | No |
| Status and Care Village | No | Yes | No |
| Leaders' comment | Yes | Yes | Yes |

Everyone who was in the app before the groups were added is in Youth.

## Who can do what

There are two kinds of password. The password someone types decides what they can do.

**Volunteer** (most helpers use this one). Each group has its own volunteer password, which
only opens that group. A Kids volunteer cannot see Youth records, and the other way round.

- check people in, after confirming their details (and their parent's details) with them
- print a child's label again
- undo a check-in made by mistake
- register new people
- fix someone's details from the check-in screen or the check-in log
- set the Care Village tick (Youth) and the comment in the leaders' notes

**Admin** (leaders). One admin password works for every group.

- everything a volunteer can do
- switch to another group with the group name at the top left, without signing out
- manage all profiles on the **Profiles** page: search, edit, archive and restore
- set a youth's status to *Trouble maker*
- see the **Dashboard**
- set each group's volunteer password on the **Settings** page

## Leaders' notes on a profile

Near the bottom of each person's details there is a **Leaders' notes** section. Kids and
Young Adults only have the comment. Youth also have a status and Care Village:

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

Please remember these notes are about children. Anyone with the group's volunteer password
can read them. The app does not record who changed a note, or when. The comment is never
printed on a label.

## Kids name labels

When a child checks in, the app can print a name label to stick on them. It shows the child's
name, grade, their parent's name and cell number, and any medical notes (allergies, asthma,
medication) in a black bar. It is made for the **DYMO LabelWriter 450** with **30321 Large
Address labels** (89 x 36 mm).

Only the computer the printer is plugged into can print. Phones and tablets cannot. To set it
up on that computer:

1. Install the DYMO software from dymo.com so Windows sees the printer, and load the labels.
2. In Windows printer settings, make the LabelWriter the **default printer**. In its printing
   preferences, choose the 30321 label size.
3. Open the app in Chrome on that computer, sign in to **Kids**, and tick **This computer
   prints name labels** under the search box. Each device remembers this for itself.
4. Check a child in. Chrome shows its print window: choose the LabelWriter, set margins to
   *None*, and print. Chrome remembers these for next time.

To skip the print window entirely, start Chrome on that computer with `--kiosk-printing`
added to its shortcut (right-click the shortcut, Properties, add it to the end of *Target*).
Labels then go straight to the default printer.

**Print label** on a child's details prints another copy at any time, from any computer that
has the printer.

## Dashboard (admins only)

The **Dashboard** page shows how the group you are signed in to is doing over any stretch of
time. Pick a date range, or use the *Last 6 weeks*, *Last 12 weeks* and *Last year* buttons.

It shows:

- how many checked in today
- how many different people came in that time
- the average number per evening (per session for Kids and Young Adults)
- how many new people registered
- attendance for each evening, and a breakdown by grade (not for Young Adults)
- **Came most often.** The people who showed up the most, out of the evenings you held.
- **Haven't come in this period.** People still on the books who did not come. It shows when
  each was last seen, or "Never checked in". The longest absences are first. Click a name to
  open their profile.
- **Export data.** Opens the Export page (see below).

## Export data (admins only)

Open **Export data** from the Dashboard. There are two spreadsheet (CSV) downloads for the
group you are signed in to, each with every detail that group records. Choose what to
include, check the number of rows it shows, then press **Download CSV**.

- **Check-in history.** One line per arrival, with the person's full details. Filter by
  dates, grade, and whether to include archived people.
- **Group list.** One line per person, with their details, leaders' notes, total check-ins,
  and first and last check-in. Filter by active or archived, grade, and (for Youth) leaders'
  notes: trouble makers or Care Village.

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

You set the volunteer passwords later, from inside the app (step 5).

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

**5. Set the volunteer passwords.** Sign in with the admin password, open **Settings**, pick
a group and type its volunteer password. Do this for each group. A group's volunteers cannot
sign in until it has one.

## Giving it an easy web address

Typing an address like `192.168.0.50:8080` is easy to get wrong. Whoever looks after the
church network can set up a friendly name such as `checkin` instead. The install guide written
for them explains how.

## Everyday use

- **Stop it:** `docker compose down`. Your data is safe. It is not kept inside the app.
- **Start it again:** `docker compose up -d`
- **Update it after a change:** `docker compose up -d --build`
- **Change a volunteer password:** sign in as an admin, open **Settings** and pick the group.
  Every device signed in as a volunteer of that group is signed out and needs the new
  password. Other groups are not affected.
- **Change the admin password:** do step 1 again with the new password, paste in the new
  code, then run `docker compose up -d`.
- **Updating from an older version:** if your `docker-compose.yml` still has a
  `CheckInAuth__VolunteerPasswordHash` line, the app copies that volunteer password across
  to Youth the first time it starts. After that you can delete the line. Changes to it do
  nothing.
- **Updating to 2.0.0 (Kids and Young Adults):** press **Back up now** on Settings
  first. The update reshapes the database once. Everyone already on file becomes Youth,
  and the Youth volunteer password keeps working.

## Where the data lives

All the details and check-ins are kept in a storage area Docker looks after, called
`sgy_checkin_data`. It stays put when you stop, start or update the app.

## Backups

Once a week the app saves a full copy of the database by itself. It needs no setup. Each copy
holds everything as it was at that moment: every person in every group, every check-in,
leaders' notes, and settings including the volunteer passwords.

The app keeps the 12 newest copies and deletes older ones. With only the weekly copies, that is
about three months. Copies made with **Back up now**, and the copy made before every restore,
count toward the 12 too, so pressing it many times pushes the older weekly copies out.

A copy can only bring back what was there when it was made. If a check-in is deleted or a note
is changed by mistake, and nobody notices until after the oldest kept copy, the old version is
gone. To keep a longer history, raise `Backup__Keep` (see `docker-compose.yml`), or keep
downloaded copies as described below.

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

Test passwords for your own computer only: **admin123** (admin, every group) and
**checkin123** (Youth volunteer, on a fresh database). Set the Kids and Young Adults volunteer
passwords on the Settings page.

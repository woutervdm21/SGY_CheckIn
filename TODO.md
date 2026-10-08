# To do

Later, not started yet.

- [ ] **Migrate Kids → Youth.** When a kid who has finished Grade 7 checks in, ask in a popup whether to move them to Youth as Grade 8. Keep their check-in history (at the moment a person's group can't change).
- [ ] **Migrate Youth → Young Adults.** Do the same for youth who have finished school when they check in: a popup to move them to Young Adults.
- [ ] **Import from CSV.** Let an admin import people for Kids, Youth and Young Adults from a CSV file, into the group they're signed in to. Use the same columns as that group's people export, so an export can be edited and imported back. Show a preview first: what will be added, rows with mistakes (missing name, a date of birth that can't be read), and likely duplicates (same name, surname and date of birth), which are skipped.
- [ ] **HTTPS, so Scan to check out works on phones.** The Scan page is built (v2.2.0), but browsers only allow the camera on HTTPS. `checkin.solidground.co.za` is already set up; check whether it opens on `https://`. If not, add Caddy beside the app on the Pi to get a free Let's Encrypt certificate through the domain's DNS (no opening the Pi to the internet); needs a DNS key for the domain, set up once with Stephan. Then set `PublicAddress` and the kiosk shortcut to the `https://` address (the v2.2.0 guide covers both).

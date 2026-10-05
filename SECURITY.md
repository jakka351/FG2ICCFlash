<!-- FG2ICCFlash — Tester Present Specialist Automotive Solutions Developed in Australia 🇦🇺 MAINTAINER TODO: set your real private-report contact below (search "REPORT CONTACT"). Recommended primary channel is GitHub's private Security Advisories for this repo. -->
SECURITY POLICY
Freedom of diagnostics is not freedom to do harm. Here's where we draw the line.
Tester Present keeps the session alive. Security keeps it honest.

This project hands people the keys to their own machines. With that comes one non-negotiable duty: the keys must be safe to hold. A tool that frees you to reprogram your car must never be a tool that bricks it behind your back, or quietly turns your PC into someone else's.

I. TWO KINDS OF "SECURITY" — AND WE MEAN VERY DIFFERENT THINGS BY THEM
Most security policies pretend there's only one. For a repair tool there are two, and confusing them is how owners get locked out of their own property.

🔓 Manufacturer "security" — i.e. secrets kept from the owner
Seed-key algorithms. Memory maps. Routine IDs. Flash procedures. The stuff a dealer network guards so that consult your dealer stays the only answer.

We do not consider documenting this a vulnerability. Publishing it is the mission.

So, to be absolutely clear:

Do not send us a "responsible disclosure" that this software can unlock, read, or reprogram a module you own. That's the feature. That's the entire point of the repo.
Do not expect us to quietly report a weak factory seed-key to the factory so they can patch owners out of their own cars. We write it down. We give it away.
The server content is diagnostic and repair data for owners — not a leak to be plugged.
Knowledge that only a dealer holds is a tax on everyone else. We don't pay it, we don't collect it, and we don't treat its disclosure as a bug.

🛡️ Real security — i.e. protecting the human and their machine
This is where we are deadly serious, fast, and grateful to anyone who helps. A vulnerability here is anything in our code or our distribution chain that could:

Brick a module — a flash-sequencing bug, a wrong offset, a corrupt-but-accepted image, a procedure that leaves a module half-programmed.
Harm the operator's computer — path traversal / Zip-Slip out of a crafted image, arbitrary file writes, a poisoned download executed or trusted.
Corrupt the supply chain — a tampered firmware image or catalog that the tool would accept and write to someone's car.
Lie about success — anything that reports "verified / done" when it silently wasn't.
Those are the bugs that end with someone stranded, out of pocket, or owning a paperweight. Report those. We will move fast.

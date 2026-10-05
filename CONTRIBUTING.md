<!-- FG2ICCFlash — Tester Present Specialist Automotive Solutions Developed in Australia 🇦🇺 -->
CONTRIBUTING
The garage door is open. Grab a wrench.
Tester Present keeps the session alive by refusing to go quiet. This project stays alive the same way — by people who show up, dig in, and hand the next person a better tool than they were handed.

You don't need permission to contribute. You don't need a title, a dealer login, or a decade of experience. You need curiosity, honesty, and the discipline to not brick somebody's car. Everything else we'll figure out together.

I. YOU DON'T HAVE TO WRITE CODE TO MOVE THIS FORWARD
Some of the most valuable contributions never touch the compiler:

🔑 Reverse-engineering, with evidence. A new seed-key, a memory map, a routine ID, a block layout — gold. But show your working: how you derived it, what it unlocks, how you confirmed it.
📈 Real CAN/UDS traces. A clean .asc/.log from actual hardware is worth a thousand guesses. It's how we validate the protocol instead of hoping.
💾 Community firmware & recore images. Factory package trees, verified backups, known- good software levels — catalogued, hashed, and shared so nobody's stuck when a dealer says buy a new one.
📖 Procedures, warnings, and docs. The step you learned the hard way is the step that saves the next person their module. Write it down.
🐞 Bug reports and test results. Especially from real benches and real cars. Tell us what broke, how, and with what gear.
No contribution that makes the next repair safer or clearer is "too small."

II. THE PRIME DIRECTIVE — THIS TOOL TOUCHES LIVE HARDWARE
Read this twice. It's the one rule that outranks all the others.

Code here can permanently destroy a module. A wrong offset, a bad erase parameter, a half-tested flash path, a security routine fired in the wrong session — any of them can turn someone's ICC into a paperweight and their day into a four-figure problem.

So:

Test on a bench, a spare, a simulated channel, or against traces — not someone's daily driver. The repo ships a simulated CAN channel and validates comms against recorded on-car traces for exactly this reason. Use them.
Anything touching the flash / erase / security-access / download paths gets extra scrutiny, every time. That's not distrust of you — it's respect for the person on the other end with the bonnet up.
Never hide risk to make a PR look clean. If a change could brick something under some condition, say so, in bold, in the PR. Honesty about danger is the highest form of contribution here.
Measure twice. Flash once. Document the failure modes either way.

III. BRING A TRACE — THE EVIDENCE RULE
This is a reverse-engineering project, so we run on evidence, not vibes.

"It works now" is not a contribution. A trace, a log, a repro, or a derivation is.
New key or map? Show how you got it and how you verified it against a real module.
Protocol change? Back it with a capture. If the wire disagrees with the theory, the wire wins.
Claims are cheap. Captures are currency.

IV. BUILDING IT
Factory-grade means boring, reproducible builds. The non-obvious bits:

.NET Framework 4.8.1, WinForms, C# 7.3.

x86 is MANDATORY — not optional. The vendor J2534 PassThru DLL is 32-bit and is loaded via LoadLibrary; a 64-bit host process physically cannot load it. Build and run x86.

Build with MSBuild (VS2022 / Build Tools):

msbuild FG2ICCFlasher.csproj -t:Build -p:Configuration=Release -p:Platform=x86
Run the resulting exe as a 32-bit process. On 64-bit Windows that means launching it normally (it's marked x86) — don't wrap it in a 64-bit host.

The embedded firmware (PHFs + SBL) and the Tester Present branding ship as embedded resources; keep resource ids stable if you touch the project file.

If your change builds clean on x86 and runs against the simulated channel without drama, you're ready to open a PR.

V. WRITING CODE THAT FITS
Make it read like the code around it. Match the existing naming, structure, and idiom. A diff that looks like it was always there is a good diff.
Comments state constraints, not narration. Explain the thing the code can't say — "this must be ISO15765_PS or it fires on HS-CAN," "P2* is 5000 ms," "x86 is mandatory because…". Don't narrate what the next line obviously does.
No obfuscation. No cleverness for its own sake. The next person to read this might be debugging it at 1 a.m. with a module on the bench. Be kind to them.
Keep changes scoped. One idea per PR. A tight, reviewable change lands; a sprawling one rots in the queue.
VI. OPENING A PULL REQUEST — THE CHECKLIST
not done
Scoped to one coherent change.
not done
The why is written down — provenance for reverse-engineered facts, trace references for protocol changes.
not done
Brick-risk flagged in bold if any path near flash/erase/security/download is touched.
not done
Evidence attached — a trace, a log, a bench result, or a repro.
not done
Builds clean on x86; runs against the simulated channel.
not done
Docs/procedures updated if behaviour or steps changed.
not done
Sources credited. Name the shoulders you stood on.
VII. HOW REVIEW WORKS AROUND HERE
Expect hard, adversarial review — this project is audited by multiple reviewers (and automated adversarial passes) before releases, especially anything that could brick a module or compromise an operator's PC. That rigour is a feature, not hostility.
We argue about the work, never the worker. Rip into a bad idea; bring data. The moment it turns personal, it's gone. (See the Code of Conduct.)
Don't take review personally. A reviewer catching a risk in your code is a reviewer saving someone's car. That's a gift. Say thanks and fix it.
VIII. WHAT WE LOVE, AND WHAT GETS BOUNCED
We love: validated keys/maps with provenance · real traces · catalogued community backups with hashes · bricking-bug fixes with repros · clear safety docs · changes that make a risky step obviously risky.

We bounce: untested changes on the flash path · code that hides or downplays danger · obfuscation · copy-paste without credit · and anything built to enable fraud, theft, or deceiving the next owner of a vehicle. Freedom of diagnostics is not freedom to lie — that line is drawn in the Code of Conduct (§IV) and the Security Policy, and it is not negotiable.

IX. LICENSING & CREDIT
By contributing, you agree your work is shared under the project's licence so the next owner, tech, and tinkerer gets to stand on your shoulders the way you stood on the ones before you. In return: you get the credit. We name the people who do the work. That's the whole deal.

X. THE CREED
We show up. We dig in. We bring a trace. We leave the tool better than we found it, and the next person readier than we were. We tell the truth about risk, we credit the shoulders we stand on, and we never ship something that could brick a stranger's car to save our own face.

When the manufacturer goes silent, we keep building.

Tester present. Hands dirty.

Built in a shed, not a boardroom. Developed in Australia 🇦🇺 Tester Present — Specialist Automotive Solutions.

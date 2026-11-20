# Scam Sense: Curriculum-Based Question Bank (v1, 30 questions)

**Source documents (all read in full):**

| Code | Course title (as written in the syllabus) | Program | Short name used below |
|---|---|---|---|
| BIT1101 | Information Assurance in Security 1 | BITCF | **IAS** |
| BIT1225 / BIT1225L | Computer Security (Lec/Lab) | BITCF | **CompSec** |
| BIT2226 / BIT2226L | Cyber Forensics (Lec/Lab) | BITCF | **CyFor** |
| BIT2105 | Social and Professional Issues | BSIT (Game Development) header | **SPI** |
| BIT4233 / BIT4233L | Advanced Cybersecurity Strategies (Lec/Lab) | BITCF | **ACS** |

**How to read the alignment labels**

- **Direct**: the concept is named in the syllabus topic, objective or learning outcome.
- **Application**: the syllabus covers the general concept, such as threats, user awareness, malware or incident reporting. The question applies it to a specific scam that the syllabus does not name.
- **Adaptation**: a syllabus concept from another context is reused for a scam situation. For example, *zero trust* comes from network design and is reused here for checking a payment request.

> **Important:** none of the five syllabi uses the words *phishing*, *scam*, *social engineering* or *identity theft*. Every question about a specific scam technique is therefore an **Application** or **Adaptation** of a broader topic. No question claims that a syllabus teaches phishing by name.

**Fictional names.** Like the existing levels, all brands, domains and numbers are made up and use `.example` domains (NovaCloud, ExampleBank, PayLite, SwiftParcel and similar). `uphsl-binan.example` follows the convention already used in Level 5.

---

## PART 1: Curriculum Analysis

### Problems found in the documents (please check with the instructors)

1. **BIT4233 course description is for another course.** It describes "texture maps… bump mapping… bilinear interpolation", which is computer graphics content. Only the BIT4233 topic table was used.
2. **BIT2105 course description is for another course.** It describes "memory addressing… Assembly Language", which is computer organization content. Its header names the BSIT Game Development program, while the other four syllabi are BITCF. Only the BIT2105 weekly topics and course outcomes were used.
3. **BIT4233's assessment column does not match its rows.** For example, "Cloud Security" lists "Develop a risk management plan". Assessment tasks from BIT4233 were not used as evidence.
4. **IAS title.** The file is named "Information Assurance in Security", but the course title inside the document is "Information Assurance in Security 1" (BIT1101).

### Relevant subjects and topics

**1. Information Assurance in Security 1 (BIT1101), the most relevant subject.** Its outcomes include "identify and assess… cybersecurity threats, vulnerabilities, and associated risks" (CO3) and "analyze real-world security incidents" (CO5). These match what a Scam Sense player does.

| Week | Topic (exact) | Why it matters for Scam Sense |
|---|---|---|
| 3 | Fundamentals of Information Security (CIA Triad) | Every scam attacks confidentiality (stolen logins), integrity (changed payment details) or availability (locked accounts). |
| 7–8 | Principles of Information Assurance: confidentiality, integrity, availability, authentication, authorization, non-repudiation, accountability | Scams are mostly attacks on *authentication*: pretending to be someone and stealing the means to prove identity. |
| 9 | Information Assets and Asset Management | Players learn which of their own assets (email, e-wallet, OTPs) are most valuable to scammers. |
| 10–11 | Cybersecurity Threats and Attack Landscape | This is the closest topic to "types of scams". |
| 13–14 | Vulnerabilities and Risk Management | Supports deciding which risk to deal with first. |
| 15 | Security Controls and Protection Mechanisms (administrative, technical, physical; defense-in-depth) | MFA, verification procedures and filters are controls that stop scams. |
| 16 | Information Security Best Practices: "develop user awareness strategies… incident reporting procedures" | **This is what Scam Sense is:** a user-awareness tool, plus reporting after a mistake. |
| 17 | Real-World Applications and Case Studies | Realistic situations such as QR stickers and campus payment scams. |

**2. Computer Security (BIT1225).** It is applied and hands-on, and its first outcome is "types of security threats and vulnerabilities in both personal and organizational contexts". That covers both personal and workplace scams.

| Topic (exact) | Why it matters |
|---|---|
| Introduction to Computer Security | Threats in personal and organizational settings. |
| Cryptography | Explains what HTTPS and the padlock prove, and what they don't. |
| Network Security (firewalls, IDS/IPS, VPNs) | Public Wi-Fi traps and what a VPN can and cannot protect. |
| Authentication and Access Control | OTPs, password resets, help-desk pretexting. |
| Malware and Threats | Malicious attachments, fake "hack tools", macros. |
| Operating System Security (patch management) | Fake "virus alert" and fake update pop-ups. |
| Web and Application Security | Fake URLs, lookalike sites. |
| Incident Response and Forensics | What to do right after you've been hit. |

**3. Cyber Forensics (BIT2226).** It adds the investigation side, which fits Scam Sense's first-person *investigation* format.

| Topic (exact) | Why it matters |
|---|---|
| Digital Evidence and Evidence Collection | Saving proof after a scam (screenshots, receipts, reference numbers). |
| Network Forensics and Cybercrime Investigation | Reading email headers to find spoofing. |
| Mobile Device Forensics | Text messages, and why a sender name on a phone is not proof. |
| Legal Aspects of Cyber Forensics (evidence integrity) | Why you shouldn't edit or crop evidence. |

**4. Social and Professional Issues (BIT2105).** It adds ethics and the law. Its CO4 explicitly includes "data privacy regulations, cybersecurity awareness".

| Week | Topic (exact) | Why it matters |
|---|---|---|
| 9 | Ethics for IT Workers | A help-desk worker being pressured to break procedure. |
| 10–11 | Ethics for IT Users ("responsible use of… social media") | Oversharing, data-harvesting quizzes. |
| 13–14 | Security Incidents ("preventive and corrective measures") | Owning up to and reporting a mistake. |
| 15 | Data Privacy Act (RA 10173) | Recognizing forms that collect more personal data than they need. |
| 16 | Professional Organization and ACM Code of Ethics | Ethical response when you discover a scam operation. |

**5. Advanced Cybersecurity Strategies (BIT4233).** This is a 4th-year course, so it is used only for **Hard** questions.

| Topic (exact) | Why it matters |
|---|---|
| APT Defense (TTPs) / Advanced Pen Testing (reconnaissance) | Targeted spear phishing built from public information. |
| Secure Network Design (zero-trust) | "Never trust, always verify" applied to payment requests. |
| Modern Cryptography ("data in transit") | What TLS/HTTPS guarantees. |
| Cloud Security (identity management, SaaS) | Fake app-permission (OAuth consent) requests. |

**Topics left out on purpose:** storage, cloud and memory forensics tools; SDN and micro-segmentation; post-quantum cryptography; secure coding; DAC/MAC/RBAC models; business continuity. They are real cybersecurity topics, but a player looking at a message does not use them. Forcing them in would break the "don't force a connection" rule.

---

## PART 2: Curriculum-to-Game Mapping

| Course Subject | Curriculum Topic | Cybersecurity Connection | Possible Game Application | Questions |
|---|---|---|---|---|
| IAS (BIT1101) | Fundamentals of Information Security (CIA Triad) | Stolen logins break confidentiality | After a scam, the player names which security goal was broken | E01 |
| IAS | Principles of IA (authentication) | Password resets and sign-in alerts prove identity | Tell a real, expected reset email from a fake one | M01 |
| IAS | Information Assets and Asset Management | Email accounts and OTPs are high-value assets | Rank which account to protect first | E08 |
| IAS | Cybersecurity Threats and Attack Landscape | Impersonation and urgency scams | Family "new number" text on the pocket phone | M04 |
| IAS | Vulnerabilities and Risk Management | Likelihood × impact | Choose which finding to fix first after a phishing wave | H04 |
| IAS | Security Controls and Protection Mechanisms | MFA as a technical control | Pick the control that stops a stolen password | M06 |
| IAS | Information Security Best Practices | User awareness and reporting | Fake parcel-fee SMS | E02 |
| IAS | Real-World Applications and Case Studies | QR code payment fraud | Canteen QR sticker | H09 |
| CompSec (BIT1225) | Introduction to Computer Security | Threats in personal and organizational contexts | Fake "mailbox full" email from campus IT | E03 |
| CompSec | Authentication and Access Control | OTP theft, help-desk pretexting | Fake bank call asking for an OTP; fake dean asking for a reset | E04, M09 |
| CompSec | Malware and Threats | Trojans, macro malware | "Free skins" EXE; "Enable Content" attachment | E05, M10 |
| CompSec | Operating System Security | Fake alerts pretending to be the OS | Browser "your PC is infected" pop-up | E09 |
| CompSec | Web and Application Security | Deceptive URLs | Read a subdomain-trick URL | M02 |
| CompSec | Network Security (VPNs) | Fake (evil twin) Wi-Fi with a login trap | Café Wi-Fi asking for a social media password | M07 |
| CompSec | Incident Response and Forensics | Containment | Ransomware on a lab PC | H08 |
| CyFor (BIT2226) | Digital Evidence and Evidence Collection | Preserving proof | Save evidence after being scammed | E10 |
| CyFor | Mobile Device Forensics | SMS sender names can be faked | Fake bank text in the real bank's thread | M08 |
| CyFor | Network Forensics and Cybercrime Investigation | Email header analysis (SPF/DMARC, Reply-To) | Header viewer on the case-files computer | H06 |
| CyFor | Legal Aspects of Cyber Forensics | Evidence integrity | Prepare a scam report correctly | H05 |
| SPI (BIT2105) | Ethics for IT Users | Oversharing on social media | "Find your star name" data-harvest post | E06 |
| SPI | Data Privacy Act (RA 10173) | Collecting more personal data than needed | Raffle booth form; fake scholarship form | E07, H07 |
| SPI | Security Incidents | Corrective measures, reporting | You typed your password into a fake page at OJT | M05 |
| SPI | Ethics for IT Workers | Refusing pressure to skip verification | Help-desk caller pretending to be the dean | M09 |
| SPI | Professional Organization and ACM Code of Ethics | Authorized access, avoiding harm | You find a phishing kit's open admin page | H10 |
| ACS (BIT4233) | Modern Cryptography | What TLS proves | Padlock question | M03 |
| ACS | APT Defense / Advanced Pen Testing (reconnaissance) | Spear phishing from public information | Org treasurer targeted with real meeting details | H01 |
| ACS | Cloud Security (identity management) | Fake app-permission (OAuth consent) requests | "DocuView Plus wants to read and send your mail" | H02 |
| ACS | Secure Network Design (zero-trust) | Verify every request | Compromised executive account asks for payment | H03 |

---

## PART 3: Game Questions

**Field notes:**
- **Curriculum Link** gives the alignment label (Direct / Application / Adaptation) and the syllabus wording behind it.
- **Unity Use** says how the question fits the current build. **[Fits now]** means it can be entered as a `ScamMessage` (channel, sender, body, `correctVerdict`). **[Needs MC mode]** means it needs the four-option answer mode described in Part 5, because `QuizManager` only scores Scam/Legitimate today.

### EASY (10)

#### E01
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Week 3: Fundamentals of Information Security (CIA Triad) |
| Question ID | E01 |
| Question Type | Multiple Choice |
| Difficulty | Easy |
| Question/Scenario | Ana typed her student login into a fake "school portal" page. The scammer can now read her grades, messages and personal details. **Which part of the CIA Triad was broken?** |
| Choices | A. Availability<br>B. Integrity<br>C. Confidentiality<br>D. None, because she typed it in herself |
| Correct Answer | **C. Confidentiality** |
| Explanation | Confidentiality means only authorized people can see information. The scammer was never authorized, so confidentiality is broken. Integrity would be broken if the scammer *changed* her data, and availability if she were *locked out*. Being tricked into giving the password away does not make the access authorized. |
| Learning Objective | Connect a common scam outcome to the security principle it violates. |
| Game Feedback | "Correct. A stolen password breaks **confidentiality**: someone who shouldn't see your data now can." |
| Curriculum Link | **Direct**: Week 3 asks students to "explain the CIA Triad". The fake portal is only the setting. |
| Unity Use | [Needs MC mode]. Works well as a results-screen question after a Level 1 credential-phishing message. |

#### E02
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Week 16: Information Security Best Practices (user awareness) |
| Question ID | E02 |
| Question Type | Scam or Legitimate |
| Difficulty | Easy |
| Question/Scenario | **Case note:** You have not ordered anything this month.<br>**SMS from +63 917 555 0142:** "SwiftParcel: Your package is ON HOLD due to an incomplete address. Pay the ₱29 redelivery fee within 12 hours or it will be returned: http://swiftparcel-redeliver.example/pay" |
| Choices | Scam / Legitimate |
| Correct Answer | **Scam** |
| Explanation | You are not expecting a package. It comes from a regular mobile number, sets a 12-hour deadline, and links to a domain that isn't the courier's. The small fee is bait: the real goal is your card or e-wallet details. Couriers don't collect fees through random text links. |
| Learning Objective | Recognize the common signs of a fake delivery text: unexpected, urgent, a small fee and an odd link. |
| Game Feedback | "Scam. No order, a small 'fee' and a 12-hour deadline. They want your payment details, not ₱29." |
| Curriculum Link | **Application**: Week 16 covers "develop user awareness strategies". This is a standard awareness case that the syllabus doesn't name specifically. |
| Unity Use | [Fits now] `channel = SMS`, `correctVerdict = Scam`, put the no-order fact in `caseNotes`. |

#### E03
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Introduction to Computer Security: "types of security threats… in both personal and organizational contexts" |
| Question ID | E03 |
| Question Type | Identify the Threat |
| Difficulty | Easy |
| Question/Scenario | **Email from:** Campus IT Helpdesk &lt;helpdesk@uphs1-binan.example&gt;<br>**Subject:** Mailbox 98% full: verify now<br>"Your school mailbox will be DELETED in 24 hours. Click below and sign in to verify your account." **What kind of threat is this?** |
| Choices | A. Phishing<br>B. Ransomware<br>C. Denial-of-service attack<br>D. A normal storage warning |
| Correct Answer | **A. Phishing** |
| Explanation | It pretends to be a trusted sender, creates fear with "deleted in 24 hours", and asks you to sign in through a link. That combination is phishing. The domain is a lookalike: **uphs1** with the number 1 in place of the letter **l**. Real storage warnings don't need you to "verify" your password through an email link. |
| Learning Objective | Name phishing and spot a lookalike domain. |
| Game Feedback | "Phishing. Look closely: **uphs1**, not uphsl. A fake sender plus a fake deadline plus 'sign in here' means phishing." |
| Curriculum Link | **Application**: the ILO asks students to "identify and explain various types of security threats". Phishing is one such threat, though the syllabus doesn't name it. |
| Unity Use | [Fits now] as a Scam email, using the `senderDetail` inspectable for the domain. [Needs MC mode] to ask for the threat name. |

#### E04
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Authentication and Access Control |
| Question ID | E04 |
| Question Type | What Should You Do? |
| Difficulty | Easy |
| Question/Scenario | A caller says: "This is ExampleBank's fraud department. Someone is using your card right now. We just sent you a 6-digit code. Read it to me so we can block the transaction." |
| Choices | A. Read the code quickly to stop the fraud<br>B. Give only the first three digits<br>C. Ask them to email you instead<br>D. Hang up and call the number on the back of your card or in the official app |
| Correct Answer | **D** |
| Explanation | A one-time code (OTP) proves that *you* are approving something. Whoever has it can approve a login or payment as you, so the caller is probably the one making the transaction. A real bank never asks you to read an OTP aloud. Calling back on a number you already trust is the safe way to check. |
| Learning Objective | Treat an OTP like a key: never share it, and verify callers through a channel you choose. |
| Game Feedback | "Hang up. **No real bank asks for your OTP.** Call back using the number on your card." |
| Curriculum Link | **Application**: the topic is "Authentication and Access Control". The syllabus focuses on access-control models, so using OTPs as an authentication factor applies the authentication part to a phone scam. |
| Unity Use | [Needs MC mode], or a future phone-call mechanic (see Part 5). |

#### E05
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Malware and Threats: "preventing, detecting, and removing malware, including… anti-malware tools" |
| Question ID | E05 |
| Question Type | Scenario-Based |
| Difficulty | Easy |
| Question/Scenario | A stranger in a gaming chat sends you **FreeSkins_Generator.exe** and says: "Turn off your antivirus first. It gives a false alarm on this tool." |
| Choices | A. Turn off the antivirus just for a minute and run it<br>B. Run it, then scan your PC afterwards<br>C. Don't download it, and block or report the sender<br>D. Send it to a friend to test first |
| Correct Answer | **C** |
| Explanation | Asking you to turn off your antivirus is a major warning sign: the file is built to get past it. Fake "generators" are a common way to spread trojans and information stealers, which take saved passwords and logged-in sessions. Scanning afterwards is too late, and sending it to a friend puts them at risk. |
| Learning Objective | Recognize that a request to disable security tools is a red flag. |
| Game Feedback | "Don't run it. Anyone who asks you to switch off your antivirus is trying to get past it." |
| Curriculum Link | **Direct**: the objective explicitly covers malware prevention and anti-malware tools. |
| Unity Use | [Fits now] as a Scam `Chat` message on the pocket phone. [Needs MC mode] for the four options. |

#### E06
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105) |
| Related Lesson/Topic | Weeks 10–11: Ethics for IT Users ("responsible use of… social media") |
| Question ID | E06 |
| Question Type | Scam or Legitimate |
| Difficulty | Easy |
| Question/Scenario | A viral post: "✨ FIND YOUR STAR NAME ✨ Comment your **first pet's name** + the **street you grew up on** + your **birth month**!" Is this harmless fun or a scam? |
| Choices | Legitimate (harmless fun) / Scam (data harvesting) |
| Correct Answer | **Scam (data harvesting)** |
| Explanation | Pet names, childhood streets and birth months are common answers to account security questions. Posting them publicly gives scammers what they need to reset your passwords or make an impersonation more convincing. The "game" is how they collect the answers. |
| Learning Objective | Recognize that harmless-looking posts can collect personal data. |
| Game Feedback | "Data harvesting. Those are classic **security-question answers**. Don't post them." |
| Curriculum Link | **Application**: Ethics for IT Users covers responsible social media use. Data-harvesting posts are an applied case. |
| Unity Use | [Fits now] as a `Website` or `Chat` message with `correctVerdict = Scam`. |

#### E07
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105) |
| Related Lesson/Topic | Week 15: Data Privacy Act (RA 10173) |
| Question ID | E07 |
| Question Type | Multiple Choice |
| Difficulty | Easy |
| Question/Scenario | A raffle booth at the mall asks you to fill in a form to join. **Which of these is the biggest privacy warning sign?** |
| Choices | A. They ask for a photo of your ID and your birthdate just to join, and there is no privacy notice explaining why<br>B. The raffle is free to join<br>C. The staff wear company shirts<br>D. The draw is next week |
| Correct Answer | **A** |
| Explanation | The Data Privacy Act's principles include **transparency** (tell people why data is collected), **legitimate purpose** and **proportionality** (collect only what is needed). A raffle needs a way to contact you, not your ID and birthdate, and an ID photo plus a birthdate can be used for identity theft. With no notice, you can't know what happens to your data. |
| Learning Objective | Check whether a request for personal data is proportional and explained before handing it over. |
| Game Feedback | "An ID photo for a raffle? Too much. Ask **why they need it** and look for a privacy notice." |
| Curriculum Link | **Direct**: Week 15 asks students to "explain the principles and provisions of the Data Privacy Act of 2012". |
| Unity Use | [Needs MC mode]. |

#### E08
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Week 9: Information Assets and Asset Management ("evaluate their value and protection requirements") |
| Question ID | E08 |
| Question Type | Multiple Choice |
| Difficulty | Easy |
| Question/Scenario | **Which of your accounts deserves the strongest protection, because a scammer who controls it can take over many of your other accounts?** |
| Choices | A. Your online game account<br>B. Your main email account, which receives password-reset links<br>C. Your music streaming account<br>D. An old forum account you no longer use |
| Correct Answer | **B** |
| Explanation | Most services send password resets to your email. Whoever controls that inbox can reset the passwords of your other accounts, including social media, e-wallets and school accounts. That makes it your most valuable information asset, so it needs a unique password and MFA first. |
| Learning Objective | Value accounts by what else they unlock. |
| Game Feedback | "Your **email** is the master key. Every 'Forgot password' link goes there." |
| Curriculum Link | **Direct**: Week 9 covers identifying assets and evaluating their value and protection needs, applied here to personal accounts. |
| Unity Use | [Needs MC mode]. |

#### E09
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Operating System Security ("patch management, and security configuration") |
| Question ID | E09 |
| Question Type | Scam or Legitimate |
| Difficulty | Easy |
| Question/Scenario | While browsing, a full-screen red page appears and beeps: "⚠ SYSTEM SECURITY ALERT ⚠ Your computer is INFECTED and LOCKED. Call Certified Support at 1-800-555-0199 now. DO NOT restart your computer." |
| Choices | Scam / Legitimate |
| Correct Answer | **Scam** |
| Explanation | This is a tech-support scam. Real security warnings and updates come from your operating system's own settings and security app, not from a web page with a phone number. The "support" agent will ask for remote access or payment. Close the tab. If it won't close, end the browser with Task Manager. |
| Learning Objective | Know where real security alerts and updates come from. |
| Game Feedback | "Scam. Real alerts come from your **system settings**, never from a web page asking you to call." |
| Curriculum Link | **Application**: the OS Security topic covers patch management and configuration through official system tools. Fake alerts pretend to be those tools. |
| Unity Use | [Fits now] `channel = Website`, `correctVerdict = Scam`. |

#### E10
| Category | Details |
|---|---|
| Course Subject | Cyber Forensics (BIT2226) |
| Related Lesson/Topic | Digital Evidence and Evidence Collection ("collection, preservation, and documentation") |
| Question ID | E10 |
| Question Type | What Should You Do? |
| Difficulty | Easy |
| Question/Scenario | You paid ₱1,500 to an online seller through PayLite. The seller then blocked you and never shipped. **What should you do first?** |
| Choices | A. Delete the chat so you can move on<br>B. Post the seller's photo and threaten them publicly<br>C. Send another payment because they say it "unlocks" your refund<br>D. Save screenshots of the chat, the seller's profile link and the payment receipt with its reference number, then report to PayLite and the authorities |
| Correct Answer | **D** |
| Explanation | Evidence is what lets the platform, your e-wallet and the police act. Reference numbers and the seller's account details can help trace the money. Deleting the chat destroys proof. Public threats can backfire on you legally. "Pay more to get a refund" is a second scam. |
| Learning Objective | Preserve and document evidence before doing anything else. |
| Game Feedback | "Save the evidence first: chat, profile and **reference number**. Then report it." |
| Curriculum Link | **Application**: the topic teaches "proper procedures for collection, preservation, and documentation" of digital evidence, applied to a victim's own report. |
| Unity Use | [Needs MC mode]. Good as a results-screen or consequence follow-up after a missed scam. |

### MEDIUM (10)

#### M01
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Weeks 7–8: Principles of Information Assurance (authentication) |
| Question ID | M01 |
| Question Type | Scam or Legitimate |
| Difficulty | Medium |
| Question/Scenario | **Case note:** One minute ago, you clicked "Forgot password" on NovaCloud yourself. **Trusted domains (case files):** novacloud.example<br>**Email from:** NovaCloud &lt;no-reply@novacloud.example&gt;<br>"We received a request to reset your password. This link expires in 15 minutes: https://novacloud.example/reset?t=8f3k… If you didn't request this, you can ignore this email." |
| Choices | Scam / Legitimate |
| Correct Answer | **Legitimate** |
| Explanation | You started this action yourself a minute ago. The sender and link domain match the trusted domain exactly. The email doesn't ask for your password or any personal details, and it tells you it's safe to ignore if you didn't ask for it. A reset email that proves you own the inbox is how authentication is supposed to work. |
| Learning Objective | Avoid "everything is a scam" thinking: confirm real messages with evidence. |
| Game Feedback | "Legitimate. You asked for it, the domain matches, and it doesn't ask for your password." |
| Curriculum Link | **Application**: authentication is one of the principles in Weeks 7–8, and here it is used to show what a *genuine* identity check looks like. |
| Unity Use | [Fits now] `channel = Email`, `correctVerdict = Legitimate`, with the trusted domain on a `CaseFile`. |

#### M02
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Web and Application Security |
| Question ID | M02 |
| Question Type | Identify the Threat |
| Difficulty | Medium |
| Question/Scenario | A message links to: **https://examplebank.example.account-verify.example/login**. **Which is true?** |
| Choices | A. It's ExampleBank's site because "examplebank" appears first<br>B. It's safe because it uses HTTPS<br>C. It's a subdomain trick: the real domain is account-verify.example<br>D. It's a misspelled (typosquatted) bank domain |
| Correct Answer | **C** |
| Explanation | A domain is read **from the right**, ending just before the first single "/". Here that is `account-verify.example`, and everything to its left is a subdomain the attacker chose. This is not typosquatting, because nothing is misspelled. HTTPS only encrypts the connection (see M03). |
| Learning Objective | Find the real domain in a URL by reading right to left. |
| Game Feedback | "Read the domain **right to left**: account-verify.example. The bank's name is just a decoy subdomain." |
| Curriculum Link | **Application**: the topic covers web security. Deceptive URLs are a user-side web threat that the syllabus doesn't name. |
| Unity Use | [Fits now] as a Scam message with the URL as the `link` inspectable. [Needs MC mode] for the four options. |

#### M03
| Category | Details |
|---|---|
| Course Subject | Advanced Cybersecurity Strategies (BIT4233). Supporting: Computer Security (BIT1225), Cryptography |
| Related Lesson/Topic | Modern Cryptography ("secure data in transit and at rest") |
| Question ID | M03 |
| Question Type | Multiple Choice |
| Difficulty | Medium |
| Question/Scenario | A login page shows a padlock and starts with **https://**. **What does the padlock actually prove?** |
| Choices | A. The site is owned by the real company<br>B. Data between you and *this* site is encrypted, but the site itself could still be fake<br>C. The site has been checked by the government<br>D. The site cannot contain malware |
| Correct Answer | **B** |
| Explanation | HTTPS (TLS) protects data *in transit* so others on the network can't read or change it. Certificates are easy for anyone to get for their own domain, so scam sites often have padlocks too. The padlock tells you the connection is private, not who you're talking to. You still have to check the domain. |
| Learning Objective | Correct the common belief that a padlock means a site is safe. |
| Game Feedback | "The padlock means **encrypted**, not **trustworthy**. Fake sites have padlocks too. Check the domain." |
| Curriculum Link | **Direct**: the topic covers protocols that "secure data in transit" (TLS). |
| Unity Use | [Needs MC mode]. Pairs well with M02 in the same room. |

#### M04
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Weeks 10–11: Cybersecurity Threats and Attack Landscape |
| Question ID | M04 |
| Question Type | Identify the Threat |
| Difficulty | Medium |
| Question/Scenario | **SMS from an unknown number:** "Ma, si Migs 'to. Nasira phone ko, bagong number ko 'to. Pa-send naman ₱3,000 sa PayLite 0917 555 0188, emergency lang. Wag ka muna tumawag, wala pa akong load." **What attack is this?** |
| Choices | A. Impersonation ("new number" family emergency) social engineering<br>B. Malware infection<br>C. A data breach<br>D. A promotional spam message |
| Correct Answer | **A** |
| Explanation | The sender claims to be family on a "new number", invents an emergency, asks for money, and **blocks you from checking** ("don't call yet"). That is social engineering: it exploits trust and urgency rather than technology. To check, call Migs's *old* number or ask something only the real Migs would know. |
| Learning Objective | Recognize impersonation that relies on emotion and urgency, and verify through a known channel. |
| Game Feedback | "Impersonation. New number + emergency + 'don't call' means: **call the old number first.**" |
| Curriculum Link | **Application**: the topic asks students to "identify major cybersecurity threats". Social engineering is a major threat that the syllabus doesn't name. |
| Unity Use | [Fits now] as a Scam `SMS` on the pocket phone. |

#### M05
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105). Supporting: IAS Week 16 (incident reporting procedures) |
| Related Lesson/Topic | Weeks 13–14: Security Incidents ("recommend appropriate preventive and corrective measures") |
| Question ID | M05 |
| Question Type | What Should You Do? |
| Difficulty | Medium |
| Question/Scenario | During your OJT, you typed your work password into a page you opened from an email. A minute later, you notice the address was wrong. |
| Choices | A. Say nothing, since nothing bad has happened yet<br>B. Forward the email to coworkers to ask if they got it too<br>C. Delete the email so no one finds out<br>D. Change the password through the official site right away, report to IT with the email, and watch for login or MFA prompts you didn't start |
| Correct Answer | **D** |
| Explanation | Fast reporting limits the damage: IT can lock sessions, block the site and warn others. Changing the password quickly makes the stolen one useless. Hiding the mistake is unprofessional and gives the attacker time. Forwarding spreads the dangerous link. Deleting it removes evidence IT needs. |
| Learning Objective | Report honestly and quickly after a security mistake. |
| Game Feedback | "Report it. Mistakes happen, and **hiding them** is what makes them serious." |
| Curriculum Link | **Direct**: Weeks 13–14 cover analyzing incidents and recommending "corrective measures". IAS Week 16 covers "incident reporting procedures". |
| Unity Use | [Needs MC mode]. Fits the existing `WrongAnswerConsequence` follow-up. |

#### M06
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Week 15: Security Controls and Protection Mechanisms ("administrative, technical, and physical controls") |
| Question ID | M06 |
| Question Type | Multiple Choice |
| Difficulty | Medium |
| Question/Scenario | A classmate's school account password was stolen through phishing. **Which control is most likely to stop the attacker from logging in?** |
| Choices | A. A stronger Wi-Fi password at home<br>B. A privacy screen protector<br>C. Multi-factor authentication (MFA) on the account<br>D. Clearing the browser history |
| Correct Answer | **C** |
| Explanation | MFA is a **technical control**: the attacker needs a second factor (an app prompt, code or security key) as well as the password. The other choices don't affect a remote login. MFA is not perfect: only approve prompts that you started yourself. |
| Learning Objective | Pick the control that matches the threat. |
| Game Feedback | "MFA. A stolen password alone isn't enough, **if you never approve a prompt you didn't start.**" |
| Curriculum Link | **Direct**: Week 15 asks students to "select appropriate controls for identified risks". |
| Unity Use | [Needs MC mode]. |

#### M07
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Network Security ("firewalls, intrusion detection/prevention systems (IDS/IPS), and VPNs") |
| Question ID | M07 |
| Question Type | Scenario-Based |
| Difficulty | Medium |
| Question/Scenario | At a café you see two networks: **Kape_FreeWiFi** and **Kape_Free_WiFi**. After you connect to one, a page says: "Log in with your SocialBook email and password to get internet access." |
| Choices | A. Don't enter it. Ask the staff for the real network name, and use mobile data for anything sensitive<br>B. Enter it, since cafés often ask you to log in<br>C. Try both networks until one works without a login<br>D. Turn off your firewall so the network connects faster |
| Correct Answer | **A** |
| Explanation | Two networks with almost the same name suggests a fake "evil twin" hotspot. A network page has no reason to ask for a *different* site's password. That is credential harvesting. **A VPN wouldn't help here:** it encrypts your traffic, but it can't protect a password you type into a fake page yourself. Turning off the firewall removes protection. |
| Learning Objective | Spot fake hotspots and understand the limits of VPNs. |
| Game Feedback | "Fake hotspot. No Wi-Fi network needs your **social media password**. Ask staff, or use mobile data." |
| Curriculum Link | **Application**: the topic evaluates firewalls and VPNs. The question applies this to choosing a network safely. |
| Unity Use | [Needs MC mode]. Could be a `Website` captured page with `correctVerdict = Scam` [Fits now]. |

#### M08
| Category | Details |
|---|---|
| Course Subject | Cyber Forensics (BIT2226) |
| Related Lesson/Topic | Mobile Device Forensics |
| Question ID | M08 |
| Question Type | Scam or Legitimate |
| Difficulty | Medium |
| Question/Scenario | A text appears **in the same thread** as your real past messages from ExampleBank: "ExampleBank: Your account will be suspended TODAY. Update your details at examplebank-ph.example/update" **Trusted domain (case files):** examplebank.example |
| Choices | Scam / Legitimate |
| Correct Answer | **Scam** |
| Explanation | Text sender names (sender IDs) can be faked, and a faked one lands in the same thread as the real sender's messages. Where the message appears doesn't prove where it came from. The evidence is in the content: `examplebank-ph.example` isn't the trusted domain, and it threatens a same-day suspension. Many banks have also announced that they no longer send clickable links by text. |
| Learning Objective | Learn that what a phone displays is not proof of who sent a message. |
| Game Feedback | "Scam. Sender names can be **faked**, so the thread proves nothing. Check the link: it's not examplebank.example." |
| Curriculum Link | **Application**: Mobile Device Forensics teaches analyzing smartphone data. The question applies the idea that phone records need checking. |
| Unity Use | [Fits now] `channel = SMS`, `correctVerdict = Scam`. A good use of the existing pocket phone. |

#### M09
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105). Supporting: Computer Security (BIT1225), Authentication and Access Control |
| Related Lesson/Topic | Week 9: Ethics for IT Workers |
| Question ID | M09 |
| Question Type | Scenario-Based |
| Difficulty | Medium |
| Question/Scenario | You're a student assistant at the IT help desk. A caller says: "This is Dean Reyes. I'm locked out and presenting in five minutes. Reset my password and send it to my personal email, deanreyes.personal@freemail.example. Hurry!" |
| Choices | A. Do it, because the dean outranks you<br>B. Reset it but send the password by SMS instead<br>C. Give the caller your own login for now<br>D. Follow the identity-verification procedure: call back on the dean's directory number, and send resets only to the official account |
| Correct Answer | **D** |
| Explanation | This is **pretexting**: a made-up story that uses authority and time pressure to get you to skip procedure. A reset sent to a personal address could hand the account to an attacker. An IT worker's duty is to follow verification even when it's uncomfortable. A real dean would understand. |
| Learning Objective | Follow verification procedures under pressure from authority and urgency. |
| Game Feedback | "Verify first. **Authority plus urgency** is the oldest trick there is. Call back on the official number." |
| Curriculum Link | **Direct**: Week 9 asks students to "evaluate the ethical responsibilities of IT professionals in the workplace". |
| Unity Use | [Needs MC mode]. Could also be a `Chat` message requesting the reset, with `correctVerdict = Scam` [Fits now]. |

#### M10
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Malware and Threats |
| Question ID | M10 |
| Question Type | Identify the Threat |
| Difficulty | Medium |
| Question/Scenario | An email titled "Revised grading sheet" has **Grades_Final.docm** attached. When opened, a yellow bar says: "Content is disabled. Click **ENABLE CONTENT** to view the grades." **What's going on?** |
| Choices | A. A normal Word update<br>B. A macro malware trap: enabling content runs hidden code<br>C. A corrupted file that needs repair<br>D. The document is just read-only |
| Correct Answer | **B** |
| Explanation | `.docm` files can contain macros, which are small programs. Office disables them by default for safety. Attackers make the document look empty or blurred so you click "Enable Content", which runs their code and can install malware. A grades file doesn't need macros to display. |
| Learning Objective | Recognize macro-enabled attachments and the "enable content" trick. |
| Game Feedback | "Macro trap. **Enable Content = run their code.** Grades don't need macros." |
| Curriculum Link | **Direct**: the topic covers preventing and detecting malware. Macro malware is one kind. |
| Unity Use | [Fits now] as a Scam email with an attachment inspectable. [Needs MC mode] for the threat name. |

### HARD (10)

#### H01
| Category | Details |
|---|---|
| Course Subject | Advanced Cybersecurity Strategies (BIT4233) |
| Related Lesson/Topic | Advanced Pen Testing (reconnaissance) and APT Defense (TTPs) |
| Question ID | H01 |
| Question Type | Identify the Threat |
| Difficulty | Hard |
| Question/Scenario | You're your student org's treasurer. **Email from:** "Prof. L. Santos" &lt;lsantos.adviser@freemail.example&gt;<br>"Hi! As we discussed at Tuesday's meeting, please send the venue deposit for the **Sept 14 Acquaintance Party** to our new account before 3 PM today. Thanks!"<br>**Case file:** The meeting, the date and the event were all posted on the org's public page. The adviser's school address is lsantos@uphsl-binan.example. |
| Choices | A. Random spam sent to thousands of people<br>B. Clone phishing (a copy of an earlier real email)<br>C. Spear phishing built from public information gathered beforehand<br>D. Ransomware |
| Correct Answer | **C** |
| Explanation | The email uses real names, dates and events, which takes research. That is **reconnaissance**, the first stage of both attacks and penetration tests. A targeted message aimed at one person is **spear phishing**. It isn't clone phishing because no earlier real email was copied. The giveaways are the free-mail address, the "new account" and the same-day deadline. |
| Learning Objective | Understand that convincing personal details can come from public sources and don't prove the sender is real. |
| Game Feedback | "Spear phishing. They read your **public page** first. Real details don't mean a real sender." |
| Curriculum Link | **Application**: Advanced Pen Testing teaches "reconnaissance", and APT Defense teaches analyzing attacker TTPs. This question looks at reconnaissance from the target's side. |
| Unity Use | [Fits now] Scam email plus a `CaseFile` with the public post. [Needs MC mode] for the technique name. |

#### H02
| Category | Details |
|---|---|
| Course Subject | Advanced Cybersecurity Strategies (BIT4233) |
| Related Lesson/Topic | Cloud Security ("identity management" in SaaS) |
| Question ID | H02 |
| Question Type | Scenario-Based |
| Difficulty | Hard |
| Question/Scenario | You open a "shared document" link. It takes you to the **real** NovaCloud sign-in page (novacloud.example). After you sign in, a screen appears:<br>"**DocuView Plus** (unverified publisher) wants to: • Read, send and delete your mail • Access your files at any time • Keep access when you're not using it" |
| Choices | A. Cancel. It's a permission-granting (consent) phishing attempt. Report it and review your connected apps<br>B. Accept, because the sign-in page was real<br>C. Accept, then change your password afterwards to be safe<br>D. Accept just this once |
| Correct Answer | **A** |
| Explanation | This attack doesn't steal your password. It gets **you** to give a malicious app permanent access. A document viewer has no reason to send or delete your mail. Changing your password afterwards often **doesn't** remove an app's access, which stays until you remove the app from your account. The sign-in page being real is part of the trick. |
| Learning Objective | Read app permission requests and know that granting them can bypass passwords. |
| Game Feedback | "Cancel. The login was real, but the **permissions** were the trap. A viewer doesn't need to send your mail." |
| Curriculum Link | **Application**: Cloud Security covers "identity management" in SaaS. App permission grants are part of identity management. |
| Unity Use | [Needs MC mode]. A good candidate for a new "consent screen" mini-game (Part 5). |

#### H03
| Category | Details |
|---|---|
| Course Subject | Advanced Cybersecurity Strategies (BIT4233) |
| Related Lesson/Topic | Secure Network Design ("zero-trust models") |
| Question ID | H03 |
| Question Type | What Should You Do? |
| Difficulty | Hard |
| Question/Scenario | An email comes from the finance director's **real** address in an existing thread, and it **passes** all email authentication checks: "Our supplier changed banks. Please pay today's invoice (₱480,000) to the new account below. I'm in meetings all day, so don't call, just reply here." |
| Choices | A. Pay it, since the email passed authentication and came from the real address<br>B. Reply to the email asking the director to confirm<br>C. Pay half now and half after confirming<br>D. Verify through a separate channel you already trust (the director's directory number and the supplier's number on file) before changing any bank details |
| Correct Answer | **D** |
| Explanation | When the director's account itself has been taken over, every technical check passes because the email really comes from that account. This is business email compromise. Replying goes straight to the attacker. Zero trust means **never trust, always verify** each request, especially a change of bank details, even when it comes from inside. "Don't call" is a warning sign in itself. |
| Learning Objective | Verify high-risk requests through a separate channel, even when they come from trusted accounts. |
| Game Feedback | "Verify by **phone, on a number you already have**. A real account can still be controlled by an attacker." |
| Curriculum Link | **Adaptation**: zero trust is taught as a network design principle. Here it is applied to people and procedures. |
| Unity Use | [Needs MC mode]. Also a good verdict-only Scam message in Level 5 [Fits now]. |

#### H04
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Weeks 13–14: Vulnerabilities and Risk Management ("conduct a basic risk assessment") |
| Question ID | H04 |
| Question Type | Multiple Choice |
| Difficulty | Hard |
| Question/Scenario | After a phishing wave on campus, IT lists four findings. **Which should be fixed first?** |
| Choices | A. Lab PCs are one month behind on non-security feature updates<br>B. The email administrator account has no MFA, and its password appears in a public breach list<br>C. The spam filter sometimes flags newsletters by mistake<br>D. Some staff haven't attended this year's awareness seminar yet |
| Correct Answer | **B** |
| Explanation | Risk = **likelihood × impact**. The admin account is very likely to be exploited, because its password is already public and there's no second factor. The impact is severe: an admin email account controls everyone's mail. The other findings are real but lower on one or both factors. Staff training matters, but it doesn't close an open door right now. |
| Learning Objective | Rank risks by likelihood and impact instead of by what seems most visible. |
| Game Feedback | "Fix the admin account first: **public password + no MFA + admin access** means high likelihood and high impact." |
| Curriculum Link | **Direct**: Weeks 13–14 cover risk assessment and risk treatment. |
| Unity Use | [Needs MC mode]. Could be a drag-to-rank risk matrix (Part 5). |

#### H05
| Category | Details |
|---|---|
| Course Subject | Cyber Forensics (BIT2226) |
| Related Lesson/Topic | Legal Aspects of Cyber Forensics ("maintaining evidence integrity"). Supporting: Digital Evidence and Evidence Collection |
| Question ID | H05 |
| Question Type | Scenario-Based |
| Difficulty | Hard |
| Question/Scenario | A friend was scammed through a chat app and wants to file a report. **Which way of handling the evidence is best?** |
| Choices | A. Crop the screenshots to just the scam message and blur the scammer's name for privacy<br>B. Forward everything to a big group chat so more people can confirm it<br>C. Keep the full conversation untouched on the original phone. Take full screenshots showing the sender's profile or number with dates and times, save receipts with reference numbers, share only copies, and note when and how each item was collected<br>D. Uninstall the app to stop the scammer and write down what you remember |
| Correct Answer | **C** |
| Explanation | Evidence has to be **complete** and **unchanged** to be trusted. Cropping removes context, and blurring removes the scammer's identifiers that investigators need. Keeping the original device and noting how you collected each item is a basic **chain of custody**. Uninstalling may delete the chat history. Group chats spread personal data. |
| Learning Objective | Keep evidence complete, unchanged and documented. |
| Game Feedback | "Keep the **originals untouched**, share copies, and write down when you saved each one." |
| Curriculum Link | **Direct**: the topic stresses "the importance of maintaining evidence integrity". The collection topic covers preservation and documentation. |
| Unity Use | [Needs MC mode]. Good for a Level 4 (Digital Forensics Lab) final case. |

#### H06
| Category | Details |
|---|---|
| Course Subject | Cyber Forensics (BIT2226) |
| Related Lesson/Topic | Network Forensics and Cybercrime Investigation ("identify signs of cybercrimes") |
| Question ID | H06 |
| Question Type | Identify the Threat |
| Difficulty | Hard |
| Question/Scenario | Email header excerpt:<br>`From: "Registrar's Office" <registrar@uphsl-binan.example>`<br>`Reply-To: registrar.office.ph@freemail.example`<br>`Return-Path: <bounce@mailer-xyz.example>`<br>`Authentication-Results: spf=fail; dkim=none; dmarc=fail`<br>`Subject: FINAL reminder: unpaid tuition, account on hold`<br>**What is the strongest evidence that this is spoofed?** |
| Choices | A. SPF and DMARC fail for the registrar's domain, and replies are redirected to a free-mail address<br>B. The subject mentions tuition<br>C. The display name says "Registrar's Office"<br>D. It was sent at 2:14 AM |
| Correct Answer | **A** |
| Explanation | The "From" line can say anything, so the display name proves nothing. **SPF/DMARC fail** means the registrar's domain did not authorize the server that sent this email. The **Reply-To** quietly sends your answer to the attacker. The subject and time are only weak signs. |
| Learning Objective | Use technical header evidence rather than what the email shows on the surface. |
| Game Feedback | "SPF and DMARC **failed**, and replies go to free-mail. The 'From' name is just text anyone can type." |
| Curriculum Link | **Application**: the topic covers analyzing network evidence for signs of cybercrime. Email headers are network evidence, though the syllabus doesn't name them. |
| Unity Use | [Fits now] as a Scam email with a "View headers" inspectable (`MessageEvidence`). |

#### H07
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105) |
| Related Lesson/Topic | Week 15: Data Privacy Act (RA 10173) |
| Question ID | H07 |
| Question Type | Scam or Legitimate |
| Difficulty | Hard |
| Question/Scenario | A "**Scholarship Grant 2027**" form is shared in a student group. It uses the school logo and asks for: full name, student number, birthdate, **mother's maiden name**, front and back of your ID, **a selfie holding your ID**, and your **PayLite MPIN** "for fast release of funds". There is no privacy notice. The only contact is a mobile number. |
| Choices | Scam / Legitimate |
| Correct Answer | **Scam** |
| Explanation | Real scholarship offices sometimes do ask for ID, which is what makes this hard. The deciding signs are: **(1)** no one ever needs your MPIN to *send* you money; **(2)** an ID plus a selfie holding it is exactly what's needed to open accounts in your name; **(3)** there is no privacy notice and no official contact. That breaks the DPA principles of **transparency** and **proportionality**. Check through the scholarship office's official channel. |
| Learning Objective | Separate normal data requests from identity-theft collection using DPA principles. |
| Game Feedback | "Scam. **No one needs your MPIN to pay you.** With ID + selfie + MPIN, they can become you." |
| Curriculum Link | **Direct**: Week 15 covers applying the Data Privacy Act's "requirements in protecting personal… information". |
| Unity Use | [Fits now] `channel = Website` captured form, `correctVerdict = Scam`, each field clickable. |

#### H08
| Category | Details |
|---|---|
| Course Subject | Computer Security (BIT1225) |
| Related Lesson/Topic | Incident Response and Forensics |
| Question ID | H08 |
| Question Type | What Should You Do? |
| Difficulty | Hard |
| Question/Scenario | After you open an email attachment on a lab PC, your files start changing to **.locked** and a note demands cryptocurrency. **What do you do first?** |
| Choices | A. Pay quickly before the price goes up<br>B. Disconnect the PC from the network (unplug the cable or turn off Wi-Fi), leave it on, stop using it, and report to IT or the incident team immediately<br>C. Restart, run a cleanup tool and delete the ransom note<br>D. Copy your files to your USB drive to save them |
| Correct Answer | **B** |
| Explanation | First, **contain** it: cutting the network connection stops it spreading to shared drives and other PCs. Leaving the PC on keeps evidence in memory for investigators. Follow your organization's response plan if it says otherwise. Paying doesn't guarantee you'll get your files back. Restarting or cleaning can destroy evidence. Copying to a USB can spread the infection or copy already-locked files. |
| Learning Objective | Know the first response steps: contain, preserve evidence, report. |
| Game Feedback | "**Disconnect, don't shut down, report.** Containment first; investigators need the evidence." |
| Curriculum Link | **Direct**: the objective covers incident response and collecting evidence in security incidents. |
| Unity Use | [Needs MC mode]. Good as a `SecurityIncident` consequence after a wrong answer on M10. |

#### H09
| Category | Details |
|---|---|
| Course Subject | Information Assurance in Security 1 (BIT1101) |
| Related Lesson/Topic | Week 17: Real-World Applications and Case Studies |
| Question ID | H09 |
| Question Type | Scam or Legitimate (scenario) |
| Difficulty | Hard |
| Question/Scenario | The canteen's "Scan to pay with PayLite" poster looks normal, but up close the QR code is a **sticker placed on top of** the printed one. Scanning it opens **paylite-merchant-pay.example/canteen** in your browser and asks for your mobile number and **MPIN**. |
| Choices | Scam / Legitimate |
| Correct Answer | **Scam** |
| Explanation | This is QR code phishing. A sticker over the original is the physical clue. A real merchant QR opens **inside the e-wallet app** and shows the merchant's name for you to confirm. It never asks for your MPIN on a web page. Also tell the canteen staff, so the next person doesn't get caught. |
| Learning Objective | Treat QR codes like links: check where they lead, and look for physical tampering. |
| Game Feedback | "Scam. Sticker over sticker, a browser page and an MPIN request. Real payments happen **inside the app**." |
| Curriculum Link | **Application**: Week 17 asks students to "analyze major cybersecurity incidents… derive lessons learned". QR fraud is a real-world case that the syllabus doesn't name. |
| Unity Use | [Fits now] as an inspectable poster or `Website` message. Physical inspection suits Scam Sense's first-person rooms well. |

#### H10
| Category | Details |
|---|---|
| Course Subject | Social and Professional Issues (BIT2105) |
| Related Lesson/Topic | Week 16: Professional Organization and ACM Code of Ethics |
| Question ID | H10 |
| Question Type | Scenario-Based |
| Difficulty | Hard |
| Question/Scenario | You find a fake school login page. Its admin page is publicly open and shows a list of **student passwords** it has captured. **What is the ethical action?** |
| Choices | A. Download the list and message each victim yourself<br>B. Post the link on the freedom wall to warn everyone<br>C. Test it with your own real password to confirm it works<br>D. Report it right away to the school's IT or security office with screenshots and the URL written in a non-clickable form (e.g., hxxp://), without logging in or using the captured data |
| Correct Answer | **D** |
| Explanation | The ACM Code of Ethics asks computing professionals to **avoid harm (1.2)**, **respect privacy (1.6)** and **access computing resources only when authorized (2.8)**. Downloading the list means handling other people's stolen data without authorization. Posting the link sends more victims to it. Testing it hands over your own password. Reporting lets the people with authority act. |
| Learning Objective | Respond to discovered wrongdoing within your authority and without causing more harm. |
| Game Feedback | "Report it, don't touch it. Good intentions don't make **unauthorized access** okay." |
| Curriculum Link | **Direct**: Week 16 covers applying "the ACM Code of Ethics… in addressing ethical issues in computing practice". |
| Unity Use | [Needs MC mode]. Strong final-room ethics question. |

---

## PART 4: Curriculum Alignment Summary

### Coverage

| Subject | Easy | Medium | Hard | Total | Topics used |
|---|---|---|---|---|---|
| IAS (BIT1101) | E01, E02, E08 | M01, M04, M06 | H04, H09 | **8** | 8 of 10 content topics |
| Computer Security (BIT1225) | E03, E04, E05, E09 | M02, M07, M10 | H08 | **8** | 7 of 9 topics |
| Social and Professional Issues (BIT2105) | E06, E07 | M05, M09 | H07, H10 | **6** | 5 topics |
| Cyber Forensics (BIT2226) | E10 | M08 | H05, H06 | **4** | 4 topics |
| Advanced Cybersecurity Strategies (BIT4233) | — | M03 | H01, H02, H03 | **4** | 5 topics |
| **Total** | **10** | **10** | **10** | **30** | |

| Format | Count | IDs |
|---|---|---|
| Multiple Choice | 6 | E01, E07, E08, M03, M06, H04 |
| Scam or Legitimate | 7 | E02, E06, E09, M01, M08, H07, H09 |
| Scenario-Based | 6 | E05, M07, M09, H02, H05, H10 |
| Identify the Threat | 6 | E03, M02, M04, M10, H01, H06 |
| What Should You Do? | 5 | E04, E10, M05, H03, H08 |

| Alignment | Count |
|---|---|
| Direct | 14 (E01, E05, E07, E08, M03, M05, M06, M09, M10, H04, H05, H07, H08, H10) |
| Application | 15 |
| Adaptation | 1 (H03) |

**Fits the current build:** 14 questions can be entered today as Scam/Legitimate `ScamMessage`s: the 7 Scam-or-Legitimate items (E02, E06, E09, M01, M08, H07, H09), plus E03, E05, M02, M04, M10, H01 and H06 played verdict-only. M07, M09 and H03 can also be rewritten as verdict-only messages. The rest need the multiple-choice mode in Part 5.

### How the questions support the curriculum

- **IAS** gets the most coverage because its stated outcomes match the game's purpose: identifying threats (CO3), choosing controls (CO4) and analyzing real incidents (CO5). Week 16's "user awareness strategies" is exactly what Scam Sense is. The game can be presented as an awareness tool built from that week's content.
- **Computer Security** provides the technical "why" behind each red flag: what HTTPS proves, how macros run code, why a VPN can't save a password you type into a fake page, and why you contain an infected PC before cleaning it.
- **Cyber Forensics** turns the player into an investigator. Reading headers, distrusting sender names on a phone, and preserving evidence fit the first-person investigation format and the existing Level 4 Digital Forensics Lab.
- **Social and Professional Issues** adds what the technical subjects leave out: whether you *should* act (H10), whether you're *obliged* to report (M05), and whether a request for data is *lawful* (E07, H07, RA 10173).
- **Advanced Cybersecurity Strategies** is used only at Hard level, where its ideas show what you can't see on the surface: reconnaissance, permission grants that bypass passwords, and compromised trusted accounts.

**Difficulty roughly follows course level, judging by the course codes.** 7 of the 10 Easy questions come from the 1xxx courses (BIT1101, BIT1225). 7 of the 10 Hard questions come from the 2xxx and 4xxx courses (BIT2105, BIT2226, BIT4233). This order makes sense for a curriculum.

**What this lets you claim in the manuscript.** Scam Sense does not teach the syllabi. It is a practice space where concepts students learned in class (CIA Triad, authentication, risk, evidence integrity, the DPA, the ACM Code) are applied to situations they will face online. Questions marked Application should be described that way. Don't write that a syllabus "teaches phishing".

---

## PART 5: Recommendations

1. **Add a multiple-choice answer mode.** `QuizManager` and `ScoringSystem` score only `MessageVerdict.Scam/Legitimate`. Adding an optional `choices[4]` plus `correctChoiceIndex` to `ScamMessage` (empty = verdict mode, as now) would support all 30 questions without a second quiz system. This matches how the project already extends data, as with `caseFiles` and `inspectables`.
2. **Tag each message with its curriculum source.** An optional `curriculumRef` field, such as `"BIT1101 · Wk 15 · Security Controls"`, could be shown on the results screen ("This connects to: …"). It would make the curriculum link visible to players and panelists, and it could be exported for the Chapter 3 instrument.
3. **Two-step questions.** First a Scam/Legitimate verdict, then "Which clue gave it away?" or "What should you do now?" This rewards analysis over guessing. It fits the existing `recheckDetails` follow-up.
4. **New inspection mechanics:**
   - **URL inspector:** hover a link to show the domain highlighted right to left (M02, M08, H09).
   - **Header viewer** on the case-files computer (H06).
   - **Consent-screen mini-game:** uncheck the permissions a document viewer doesn't need (H02).
   - **Evidence bag:** drag the right items (receipt, profile link, untouched chat) into an evidence folder (E10, H05). This suits Level 4.
   - **Voice-call scenes** for phone scams (E04, M09), using the pocket phone.
   - **Physical tampering:** inspectable QR stickers and posters in the rooms (H09).
5. **Risk-ranking board** (H04): drag four findings onto a likelihood × impact grid. This directly mirrors IAS's "Risk Assessment Matrix" rubric (R-8).
6. **Suggested placement by room:** Easy → Levels 1–2; Medium → Levels 2–3; forensics questions (E10, M08, H05, H06) → Level 4 Digital Forensics Lab; H01, H03, H09, H10 → Level 5 campus setting.
7. **Keep about 30% of messages legitimate** (M01-style), so players learn to *verify*, not just distrust everything.
8. **Pre-test/post-test.** Use a mixed set of these items before the first room and after the last to measure learning gain for Chapter 4. Each item already states its learning objective.
9. **Before using the curriculum references in the paper,** confirm the two syllabus description errors (BIT4233, BIT2105) with the instructors so the manuscript doesn't quote the wrong text.

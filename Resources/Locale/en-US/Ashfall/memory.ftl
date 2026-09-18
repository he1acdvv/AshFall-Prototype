# UI and Verbs
ashfall-memory-verb-remember = Remember
ashfall-memory-verb-remember-tooltip = Recall the details of your past relationship with this person.

# Mutual hints
ashfall-memory-hint-1 = Someone is looking at you intently...
ashfall-memory-hint-2 = You feel someone's inquisitive gaze upon you.
ashfall-memory-hint-3 = Did { GENDER($target) ->
    [female] this woman hesitate for a second
   *[male] this person hesitate for a second
}, looking at you?
ashfall-memory-hint-4 = Your face seems familiar to someone here.
ashfall-memory-hint-5 = You catch a strange look of recognition directed at you.

# Recognition fallback lines
ashfall-memory-recog-pos-work-1 = Wait... Is that really you?!
ashfall-memory-recog-pos-work-2 = Hold on, I know you!
ashfall-memory-recog-pos-work-3 = What a surprise... a familiar face!

ashfall-memory-recog-pos-pers-1 = Well, well, look who it is!
ashfall-memory-recog-pos-pers-2 = No way, it's actually you!
ashfall-memory-recog-pos-pers-3 = Of all people, I certainly didn't expect to see you here... in a good way!

ashfall-memory-recog-neg-work-1 = Not { GENDER($target) ->
    [female] her...
   *[male] him...
}
ashfall-memory-recog-neg-work-2 = Damn it. That's the one person I didn't want to see.
ashfall-memory-recog-neg-work-3 = Great, old acquaintances. Mood ruined.

ashfall-memory-recog-neg-pers-1 = Oh no... not { GENDER($target) ->
    [female] her.
   *[male] him.
}
ashfall-memory-recog-neg-pers-2 = Damn. That face is all too familiar...
ashfall-memory-recog-neg-pers-3 = Can't be. Is that really { GENDER($target) ->
    [female] her?
   *[male] him?
}

ashfall-memory-recog-neutral-1 = Hm, a familiar face...
ashfall-memory-recog-neutral-2 = I've definitely seen that face somewhere before.
ashfall-memory-recog-neutral-3 = Seems like our paths have crossed before.

ashfall-memory-recog-complicated-1 = Well, well... how should I even react to this?
ashfall-memory-recog-complicated-2 = A familiar silhouette... bringing back a flood of old memories.
ashfall-memory-recog-complicated-3 = Too many memories are resurfacing...

# Recognition overrides
ashfall-memory-recognition-debt-debtor = Damn... not { GENDER($target) ->
    [female] her. I hope she forgot
   *[male] him. I hope he forgot
} about the debt...
ashfall-memory-recognition-debt-creditor = Wait... I can't believe my eyes. That's { GENDER($target) ->
    [female] my debtor!
   *[male] my debtor!
}

# Templates

# FireRescue
ashfall-memory-fire-rescue-a-text = You recognize { GENDER($target) ->
    [female] her. A few years ago you worked together on an engineering crew. During a fire, she dragged you out of a smoke-filled maintenance tunnel, risking her own neck.
   *[male] him. A few years ago you worked together on an engineering crew. During a fire, he dragged you out of a smoke-filled maintenance tunnel, risking his own neck.
} You never really got a chance to properly thank { GENDER($target) ->
    [female] her.
   *[male] him.
}
ashfall-memory-fire-rescue-a-summary = { GENDER($target) ->
    [female] Dragged
   *[male] Dragged
} you out of a burning maintenance tunnel during a fire.
ashfall-memory-fire-rescue-b-text = You remember that face. During a terrible fire in the maintenance shafts on the previous station, you dragged { GENDER($target) ->
    [female] her
   *[male] him
} through smoke and flames on your own back. It's good to see that { GENDER($target) ->
    [female] she is
   *[male] he is
} still alive and well.
ashfall-memory-fire-rescue-b-summary = You saved { GENDER($target) ->
    [female] her
   *[male] him
} from the fire in a smoke-filled maintenance shaft.

# GoodCoworkers
ashfall-memory-good-coworkers-text = You used to work on the same shift. You rarely talked about personal matters, but on the job you understood each other without words. { GENDER($target) ->
    [female] She was
   *[male] He was
} always someone you could rely on.
ashfall-memory-good-coworkers-summary = { GENDER($target) ->
    [female] A reliable coworker
   *[male] A reliable coworker
} from your previous station.

# DrinkingBuddies
ashfall-memory-drinking-buddies-text = You regularly hung out at the bar after grueling work shifts. You drank liters of cheap booze together, complained about management, and shared tall tales. Warm memories of difficult times.
ashfall-memory-drinking-buddies-summary = An old drinking buddy from after-work shifts.

# HelpedWithMove
ashfall-memory-helped-move-a-text = You remember how during an emergency relocation, { GENDER($target) ->
    [female] she helped
   *[male] he helped
} you carry heavy crates of personal belongings without asking questions. A small gesture, but it stuck with you.
ashfall-memory-helped-move-a-summary = Helped you carry your heavy luggage during a move.
ashfall-memory-helped-move-b-text = You vaguely recall helping { GENDER($target) ->
    [female] her
   *[male] him
} load and carry junk during a quarters relocation. You rarely spoke afterward, but the face stuck in your memory.
ashfall-memory-helped-move-b-summary = You once helped { GENDER($target) ->
    [female] her
   *[male] him
} carry belongings during a move.

# AcademicRival
ashfall-memory-academic-rival-a-text = That face is all too familiar. You studied together. { GENDER($target) ->
    [female] She always got top marks, showed off to instructors, and knew she was ahead. You still dislike her.
   *[male] He always got top marks, showed off to instructors, and knew he was ahead. You still dislike him.
}
ashfall-memory-academic-rival-a-summary = A smug former classmate who always got top grades.
ashfall-memory-academic-rival-b-text = You remember { GENDER($target) ->
    [female] her from your studies. She always lagged behind in rankings and shot envious glances whenever curators praised you. Amusing to see her
   *[male] him from your studies. He always lagged behind in rankings and shot envious glances whenever curators praised you. Amusing to see him
} here.
ashfall-memory-academic-rival-b-summary = A former classmate who was always jealous of your achievements.

# Debt250
ashfall-memory-debt-debtor-text = Damn it. You know { GENDER($target) ->
    [female] her.
   *[male] him.
} You still owe { GENDER($target) ->
    [female] this woman
   *[male] this person
} 250 credits from an old poker game on the previous station. The key is to act natural and hope { GENDER($target) ->
    [female] she forgot.
   *[male] he forgot.
}
ashfall-memory-debt-debtor-summary = You owe { GENDER($target) ->
    [female] her
   *[male] him
} 250 credits.
ashfall-memory-debt-creditor-text = Look who it is! { GENDER($target) ->
    [female] This person has spent years pretending to have forgotten the 250 credits debt. High time to remind her
   *[male] This person has spent years pretending to have forgotten the 250 credits debt. High time to remind him
} about old tabs.
ashfall-memory-debt-creditor-summary = Owes you 250 credits from the old days.

# StolenCredit
ashfall-memory-stolen-credit-victim-text = You remember { GENDER($target) ->
    [female] this woman all too well. You worked on a complex project together, but when supervisors arrived, she cold-bloodedly claimed
   *[male] this person all too well. You worked on a complex project together, but when supervisors arrived, he cold-bloodedly claimed
} all the credit and bonus for { GENDER($target) ->
    [female] herself.
   *[male] himself.
} The bitterness lingered.
ashfall-memory-stolen-credit-victim-summary = Took all the credit and bonus for your joint work.
ashfall-memory-stolen-credit-culprit-text = You vaguely recognize { GENDER($target) ->
    [female] this colleague. You turned in a joint project once, and management awarded the bonus to you. She sulked quite a bit
   *[male] this colleague. You turned in a joint project once, and management awarded the bonus to you. He sulked quite a bit
}, even though you simply presented the results better.
ashfall-memory-stolen-credit-culprit-summary = A former coworker who resented you over a bonus payout.

# Whistleblower
ashfall-memory-whistleblower-reported-text = Because of a report filed by { GENDER($target) ->
    [female] this snitch
   *[male] this snitch
}, you were stripped of your quarterly bonus and subjected to a humiliating security check over a minor procedural oversight. Hard to forget.
ashfall-memory-whistleblower-reported-summary = Snitched on you to management at your previous post.
ashfall-memory-whistleblower-reporter-text = You remember { GENDER($target) ->
    [female] her. On the last station, she flagrantly violated safety protocols, and you had to report it to prevent blowing up alongside her.
   *[male] him. On the last station, he flagrantly violated safety protocols, and you had to report it to prevent blowing up alongside him.
}
ashfall-memory-whistleblower-reporter-summary = You filed a safety violation report against { GENDER($target) ->
    [female] her.
   *[male] him.
}

# SameStation
ashfall-memory-same-station-text = You walked the same station corridors and saw each other almost every day, yet never properly exchanged a word. A painfully familiar face from a past life.
ashfall-memory-same-station-summary = Saw each other almost every day on the previous station.

# MutualAcquaintance
ashfall-memory-mutual-acquaintance-text = You were never close, but you had a mutual friend in logistics who often mentioned both of you over drinks. Small universe, ending up on the same rust bucket.
ashfall-memory-mutual-acquaintance-summary = You share a good mutual friend from the old station.

# AsymmetricFriendship
ashfall-memory-asymmetric-friendship-a-text = You were close friends. At least, you always thought so: you shared news, took breaks together, and backed each other up. Strange to run into { GENDER($target) ->
    [female] her
   *[male] him
} here.
ashfall-memory-asymmetric-friendship-a-summary = You considered each other close friends on the old station.
ashfall-memory-asymmetric-friendship-b-text = { GENDER($target) ->
    [female] She always thought
   *[male] He always thought
} of you as close friends. You never quite understood why — just a couple of casual smoke breaks, and { GENDER($target) ->
    [female] she clung
   *[male] he clung
} on like a barnacle.
ashfall-memory-asymmetric-friendship-b-summary = Considered you best friends, though you didn't feel the same.

# FormerPartner
ashfall-memory-former-partner-a-text = Once, there was something more than a working relationship between you. It ended badly, leaving behind awkward tension and a reluctance to stir up the past.
ashfall-memory-former-partner-a-summary = A complicated romantic history that ended on bad terms.
ashfall-memory-former-partner-b-text = A face from the past. You once tried to make things work, but it was a clear mistake. Looking back, it's hard to imagine what you ever had in common.
ashfall-memory-former-partner-b-summary = A past romantic affair best left forgotten.

# Pre-Mothballing Station Life & Cryo-sleep

# OldShiftHandover
ashfall-memory-old-shift-handover-text = During the station's golden years, you regularly relieved each other on watch. Clean monitors, neatly filled paper logs, and hot tea in the mug. Not a single reprimand from leadership.
ashfall-memory-old-shift-handover-summary = Relieved each other on duty shifts without a single reprimand.

# CafeteriaLunchTable
ashfall-memory-cafeteria-lunch-table-text = You often shared a table in the crew mess hall back when the station was bustling with real food on the menu. Debated sports, vented about superiors, and planned vacations.
ashfall-memory-cafeteria-lunch-table-summary = Shared lunch tables in the mess hall during the station's best years.

# MothballOrderRumors
ashfall-memory-mothball-order-rumors-text = You stood together in an airlock vestibule having a smoke when the first unsettling rumors of budget cuts and section mothballing began to spread. Hardly anyone believed it back then.
ashfall-memory-mothball-order-rumors-summary = Discussed the first rumors of station mothballing together.

# CryoQueueHandshake
ashfall-memory-cryo-queue-handshake-text = On the day the sector was decommissioned, you stood side by side in the long queue to the cryo pods. Before sealing the hatches, you exchanged a firm handshake and hoped for better times ahead.
ashfall-memory-cryo-queue-handshake-summary = Shook hands in the queue entering cryosleep.

# CryoPodPrepNeighbor
ashfall-memory-cryo-prep-neighbor-a-text = Your cryo pods were right next to each other. Before entering the cold fluid, panic gripped you — the terror of never waking up. { GENDER($target) ->
    [female] She spoke calmly to you and helped
   *[male] He spoke calmly to you and helped
} steady your trembling hands.
ashfall-memory-cryo-prep-neighbor-a-summary = Helped calm your nerves before entering the cryo pod.
ashfall-memory-cryo-prep-neighbor-b-text = Your preservation pods were adjacent. You remember { GENDER($target) ->
    [female] her turning pale with fright before the cryo chamber closed. You spent a few minutes calming her
   *[male] him turning pale with fright before the cryo chamber closed. You spent a few minutes calming him
} down so the procedure wouldn't be jeopardized.
ashfall-memory-cryo-prep-neighbor-b-summary = Calmed { GENDER($target) ->
    [female] her down when she panicked
   *[male] him down when he panicked
} before cryosleep.

# OvertimeDispute
ashfall-memory-overtime-dispute-a-text = In the good old days, the corporation paid double rates for holiday shifts. { GENDER($target) ->
    [female] She snagged
   *[male] He snagged
} that lucrative shift right from under your nose through dispatch contacts. Petty, but it left a bitter taste.
ashfall-memory-overtime-dispute-a-summary = Snagged a high-paying holiday overtime shift right from under your nose.
ashfall-memory-overtime-dispute-b-text = You remember { GENDER($target) ->
    [female] her disgruntled expression when dispatch awarded the double-pay holiday shift to you instead of her.
   *[male] his disgruntled expression when dispatch awarded the double-pay holiday shift to you instead of him.
} You earned those hours fair and square, but { GENDER($target) ->
    [female] she held a grudge
   *[male] he held a grudge
} for months.
ashfall-memory-overtime-dispute-b-summary = Resented you over an overtime shift on a holiday.

# Pre-Mothballing Medical

# MedicalRoutineCheckup
ashfall-memory-medical-routine-checkup-doctor-text = You remember { GENDER($target) ->
    [female] this employee
   *[male] this employee
} from annual physicals. Routine corporate procedure: checking reflexes, vision, blood pressure, and signing off on high-altitude hazard clearance.
ashfall-memory-medical-routine-checkup-doctor-summary = Handled { GENDER($target) ->
    [female] her
   *[male] his
} routine annual corporate checkup.
ashfall-memory-medical-routine-checkup-patient-text = You recognize your doctor. When the station operated by the book, you dutifully showed up every year for mandatory medical checks and endured routine jokes about radiation levels.
ashfall-memory-medical-routine-checkup-patient-summary = Your attending physician from mandatory annual checkups.

# MedicalJointSurgery
ashfall-memory-medical-joint-surgery-text = Before the shutdown, you conducted an intricate multi-hour surgery side by side in the central medbay. Pure professionalism, communicating through glances above your surgical masks. The patient survived.
ashfall-memory-medical-joint-surgery-summary = Performed an exemplary complex surgery together in medbay.

# MedicalSickLeavePass
ashfall-memory-medical-sick-leave-doctor-text = You remember how just before the shutdown, { GENDER($target) ->
    [female] she arrived
   *[male] he arrived
} at your clinic gray with exhaustion. Taking pity on { GENDER($target) ->
    [female] her
   *[male] him
}, you discreetly logged a three-day fatigue leave, sparing { GENDER($target) ->
    [female] her
   *[male] him
} corporate penalties.
ashfall-memory-medical-sick-leave-doctor-summary = Quietly granted a fictitious medical leave to save { GENDER($target) ->
    [female] her
   *[male] him
} from burnout.
ashfall-memory-medical-sick-leave-worker-text = When overtime on the old station had you on the verge of collapse, { GENDER($target) ->
    [female] this doctor took a risk and wrote
   *[male] this doctor took a risk and wrote
} you a three-day fatigue leave. Those days of bed rest saved you from a total breakdown.
ashfall-memory-medical-sick-leave-worker-summary = Quietly wrote you medical leave when you were on the verge of collapse.

# Pre-Mothballing Security

# SecurityQuietNight
ashfall-memory-security-quiet-night-text = Back when exemplary order reigned on the station, you spent quiet night watches together at the sector checkpoint. Sipped black coffee, shared jokes over secure comms, and kept the peace.
ashfall-memory-security-quiet-night-summary = Shared quiet night watches at the sector security checkpoint.

# SecurityNoiseComplaint
ashfall-memory-security-noise-officer-text = You remember { GENDER($target) ->
    [female] this resident throwing a loud party with blaring music to celebrate completing a half-year contract. You had to personally knock on the door and threaten brig time before she turned it down.
   *[male] this resident throwing a loud party with blaring music to celebrate completing a half-year contract. You had to personally knock on the door and threaten brig time before he turned it down.
}
ashfall-memory-security-noise-officer-summary = Responded to quiet hours complaints regarding a loud party in { GENDER($target) ->
    [female] her
   *[male] his
} quarters.
ashfall-memory-security-noise-resident-text = You remember how right in the middle of a farewell party, { GENDER($target) ->
    [female] this security officer showed up with a stun baton and threatened
   *[male] this security officer showed up with a stun baton and threatened
} to throw the whole gathering into the brig for disturbing the peace.
ashfall-memory-security-noise-resident-summary = Threatened to lock up your cabin party during quiet hours.

# LostKeycardFiling
ashfall-memory-lost-keycard-officer-text = You remember { GENDER($target) ->
    [female] this clumsy soul. Before the shutdown, she managed to lose her access card down a vent, and you made her fill out tedious paperwork before issuing a replacement.
   *[male] this clumsy soul. Before the shutdown, he managed to lose his access card down a vent, and you made him fill out tedious paperwork before issuing a replacement.
}
ashfall-memory-lost-keycard-officer-summary = Made { GENDER($target) ->
    [female] her
   *[male] him
} write a tedious report over a misplaced ID card.
ashfall-memory-lost-keycard-worker-text = You vividly remember { GENDER($target) ->
    [female] this badge-wearing bureaucrat. When you dropped your ID behind wall paneling, she made
   *[male] this badge-wearing bureaucrat. When you dropped your ID behind wall paneling, he made
} you fill out three pages on negligence before handing over a duplicate.
ashfall-memory-lost-keycard-worker-summary = Forced you to write a lengthy incident report for a lost ID card.

# Pre-Mothballing Cargo & Logistics

# CargoUnloadingFreighter
ashfall-memory-cargo-unloading-freighter-text = During the station's prime, heavy freighters docked every week. You worked cargo cranes and hauled crates in sync for hours, soaked in sweat under the steady whine of hydraulics.
ashfall-memory-cargo-unloading-freighter-summary = Unloaded heavy freighters in the docking bays during the station's prime.

# CargoSpecialOrder
ashfall-memory-cargo-special-order-handler-text = You remember { GENDER($target) ->
    [female] her. At her heartfelt request, you smuggled
   *[male] him. At his heartfelt request, you smuggled
} genuine Earth coffee and a box of cigars onto the station off-manifest, handing the parcel directly over.
ashfall-memory-cargo-special-order-handler-summary = Procured an off-manifest package for { GENDER($target) ->
    [female] her.
   *[male] him.
}
ashfall-memory-cargo-special-order-client-text = You will never forget how { GENDER($target) ->
    [female] this cargo worker helped
   *[male] this cargo worker helped
} you obtain a real Earth coffee delivery from a freighter — something officially unobtainable on the station for any amount of credits.
ashfall-memory-cargo-special-order-client-summary = Helped you get a rare parcel from Earth off the official books.

# CargoCustomsHold
ashfall-memory-cargo-customs-inspector-text = You remember when a suspicious personal effects crate got flagged upon { GENDER($target) ->
    [female] her
   *[male] his
} arrival. You kept the baggage locked in inspection for three days pending clearance, weathering a storm of complaints.
ashfall-memory-cargo-customs-inspector-summary = Held { GENDER($target) ->
    [female] her
   *[male] his
} personal cargo in customs inspection upon arrival.
ashfall-memory-cargo-customs-passenger-text = You remember { GENDER($target) ->
    [female] this pedantic inspector. Upon your transfer, she held
   *[male] this pedantic inspector. Upon your transfer, he held
} your personal chest in the cargo bay for three days over an alleged seal defect, leaving you to sleep on an unmade bunk.
ashfall-memory-cargo-customs-passenger-summary = Held your personal effects in customs for three days during check-in.

# Pre-Mothballing Engineering & Science

# EngineeringGeneratorCommissioning
ashfall-memory-engineering-generator-commissioning-text = You jointly commissioned the upgraded power distribution grid years before the shutdown. Stood by the main terminal, verified frequencies, and took pride in parameters staying within tolerances.
ashfall-memory-engineering-generator-commissioning-summary = Jointly commissioned the station's upgraded power distribution hub.

# EngineeringToolboxBorrow
ashfall-memory-engineering-toolbox-owner-text = Right before the engineering wing was mothballed, { GENDER($target) ->
    [female] she borrowed your calibrated multitool 'for just half an hour.' The tool was never returned — the station was evacuated, and she merely shrugged.
   *[male] he borrowed your calibrated multitool 'for just half an hour.' The tool was never returned — the station was evacuated, and he merely shrugged.
}
ashfall-memory-engineering-toolbox-owner-summary = Borrowed your best multitool before the shutdown and never returned it.
ashfall-memory-engineering-toolbox-borrower-text = You vaguely remember the awkward incident with { GENDER($target) ->
    [female] her
   *[male] his
} multitool. You borrowed it right before the sector evacuation, and in the ensuing chaos, you never got around to returning it.
ashfall-memory-engineering-toolbox-borrower-summary = Accidentally kept { GENDER($target) ->
    [female] her
   *[male] his
} multitool during the pre-mothball rush.

# ScienceCalibrationAssistance
ashfall-memory-science-calibration-engineer-text = You remember { GENDER($target) ->
    [female] this scientist struggling to calibrate a field spectrometer. You spent half a day rewiring her power lines, and in return she calculated
   *[male] this scientist struggling to calibrate a field spectrometer. You spent half a day rewiring his power lines, and in return he calculated
} an optimal cooling loop balance for you.
ashfall-memory-science-calibration-engineer-summary = Rewired { GENDER($target) ->
    [female] her
   *[male] his
} lab spectrometer in exchange for complex thermal calculations.
ashfall-memory-science-calibration-scientist-text = You remember { GENDER($target) ->
    [female] this capable engineer. When the lab spectrometer suffered power drops, she re-soldered the feed line around the main bus, saving your research, and you assisted her
   *[male] this capable engineer. When the lab spectrometer suffered power drops, he re-soldered the feed line around the main bus, saving your research, and you assisted him
} with math models.
ashfall-memory-science-calibration-scientist-summary = Fixed your lab spectrometer in exchange for cooling system calculations.

# ScienceSymposiumDebate
ashfall-memory-science-symposium-debate-text = You both remember a cross-sector symposium where you fiercely debated the emission nature of local anomalies. Neither yielded an inch before the board, but a lasting mutual respect remained.
ashfall-memory-science-symposium-debate-summary = Fiercely debated anomaly physics at a scientific symposium.

# Pre-Mothballing Transit

# TransportLoungeChess
ashfall-memory-transport-lounge-chess-text = During the endless two-week shuttle flight to this station, you regularly met in the passenger lounge to play chess. No awkward prying into the past — just pieces clicking against the hum of sublight drives.
ashfall-memory-transport-lounge-chess-summary = Played chess in the shuttle lounge during the voyage to the station.

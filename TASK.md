Read the current waiter implementation and make ONLY these targeted visual adjustments.

Do not redesign the waiter, route, tray system, Beer Order, drinking, networking, or gameplay.

1. WAITER HEIGHT / HOVER

The waiter is now proportionally correct, but visually too short.

Keep the NORMAL PlayerCharacter body/head/hand proportions exactly as they are.

Do NOT stretch the body again.

Instead, raise the entire waiter visual slightly above the grounded root so the waiter feels like a floating SHIELDS character that is standing/walking, not a tiny floor-level character.

Important:

- preserve normal character proportions
- keep the root / route architecture
- keep the current bob behavior
- do not return to the previous extremely tall waiter
- do not make the waiter look fully airborne again

Desired result:

- waiter floats only a LITTLE above the floor
- still feels grounded / walking
- noticeably taller than the current version
- clearly shorter / lower than the old stretched 1.50 m-head version

Find the best visual midpoint by tuning the waiter visual Y offset, not mesh scale.

Please test several small offsets and keep the one that looks most natural from seated player eye level.

I expect something roughly in the range of a modest 10–25 cm visual lift, but choose based on the actual scene rather than blindly using that number.

The lowest point of the bob should still be close enough to the floor that it reads as grounded movement.

2. DELIVERY HAND TRAJECTORY

The waiter body now correctly stays on the circular route.

Keep that.

The problem is the delivery hand arc is far too exaggerated.

Currently the hand rises very high, almost like a catapult, before coming down to the table.

Reduce this dramatically.

Desired motion:

tray
→ small lift only if needed
→ smooth forward/side reach
→ DrinkArea on the table
→ return along a similar path

The hand may rise enough to clear:

- tray
- chair back
- player shoulder/body

but it should NOT go above the waiter's head unless absolutely unavoidable.

Prefer a low, direct, natural service arc.

Use the floating-hand design: it can simply extend a long distance horizontally.

The hand should visually feel like a waiter reaching over and placing a mug, not throwing or launching it.

3. PRESERVE

Do not change:

- constant-radius circular waiter route
- no inward waiter-body movement
- left-rear service position
- left-side DrinkArea
- same mug object tray → hand → DrinkArea
- tray stability
- palm-up tray hand
- 3 / 6 / 8 player compatibility
- targeting safety
- multiplayer presentation
- BeerLedger / drinking

4. VALIDATE

Check from seated player viewpoints at 3, 6 and 8 players.

For waiter height:
- no stretched body
- no tiny/crouched appearance
- no extreme floating
- no floor clipping

For delivery hand:
- no catapult-like high arc
- no hand through body/head/chair
- mug reaches DrinkArea cleanly
- motion looks restrained and service-like

Run the current relevant tests and report:
- final visual Y offset
- final head-top world height
- final hand trajectory / maximum height
- minimum clearances from body/head/chair
- files modified
- test result
- console result

Do not commit anything.
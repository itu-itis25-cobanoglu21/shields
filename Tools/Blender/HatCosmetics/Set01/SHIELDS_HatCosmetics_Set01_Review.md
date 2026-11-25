# SHIELDS Hat Cosmetics Set 01

The turnaround sheet governed the fit, silhouette, and construction. The larger front sheet guided the stitches, spade card, tricorn binding, and four-pip patch.

| Outfit root | Triangles | Materials | Child meshes |
|---|---:|---:|---|
| TavernCap | 1,824 | 2 | Folded cloth crown · Folded lower band · Cross stitches |
| RogueGambler | 2,716 | 3 | Tapered felt crown · Swept brim · Oxblood hat band · Tucked spade card |
| TricornScout | 3,600 | 2 | Felt hat crown · Cocked three-corner brim · Bound ochre edge |
| DiceBandana | 4,248 | 3 | Fitted cloth crown · Gathered wrap folds · Wrapped knot · Hanging cloth tails · Four-pip dice patch |

The material counts are distinct materials per outfit. All hats share a packed swatch palette and four reusable materials for cloth, felt, trim, and card or ink.

## HeadCosmeticAnchor

The Blender anchor represents the origin of Unity's PlayerCharacter/HeadPivot/HeadVisual, the same frame used by FaceAnchor. Each outfit root and its mesh children have identity local transforms. Dimensions use metres. The Blender authoring axes are +X right, −Y forward, and +Z up. The view is in the neutral head frame, with the prefab's pitch applied by HeadPivot when integrated later.

The head reference is head_character from shields_character_v03.fbx, fitted using the current prefab's HeadMesh scale of 0.4947785 and vertical offset of 0.08 m. Its fitted dimensions are approximately 239.4 × 239.5 × 235.3 mm. The reconstructed head was checked vertex by vertex against the source FBX and prefab transform. The head, face, anchor reference, camera, and lights are marked non-export.

The HatCosmetics collection contains one child collection per root. Use the collection eye and camera icons to switch the active outfit.

## Fit and reference adjustments

- Forehead edges were lifted locally wherever the tallest SHIELDS eyebrows needed clearance. The review character uses Suspicious eyes, Mischievous brows, and Crooked Grin. Mesh intersection tests also covered the available eye, eyebrow, and mouth variants.
- The Tavern Cap crown drops to the right and has two small cross stitches. The band follows the actual brow and skull contour.
- The Rogue Gambler has a diagonal brim, a tapered crown, and one tucked spade card. The three-quarter and side views show the card side.
- The Tricorn Scout has a continuous, three-corner upturned brim and a separate bound edge. The opening and low crown were fitted to the round skull.
- The Dice Bandana wraps gathers into a rear-right knot. Its long tail kicks slightly outward so it stays readable in profile.

The review covered front, three-quarter, side, back, and an additional elevated fit view. The inside of the fitted crowns and bands can seat slightly into the head. All hats are static Blender assets awaiting visual approval.

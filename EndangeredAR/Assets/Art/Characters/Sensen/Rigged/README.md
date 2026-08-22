# Sensen Rigged Character Asset

## Authorization

The source model was created and supplied by the project owner. On 2026-08-22,
the owner authorized its unrestricted use for this project, including local
development, competition demonstrations, App distribution, and storage in the
project repository. The asset must not be represented as third-party work or as
independently verified species-accurate final art.

This repository records the owner's authorization statement. It is not an
independent chain-of-title audit. No push or public release was performed as
part of R3.0 integration.

## Approved runtime derivative

- File: `Models/sensen_rigged_100k.fbx`
- SHA-256: `c885fe83a6605e71b2d977e28871f6259647754f79861320fbf68dec0b960926`
- File size: 7,683,340 bytes
- Blender geometry: 53,815 vertices, 100,000 triangles
- Unity geometry: 54,684 vertices, 100,000 triangles
- Rig: Generic, 33 imported bones, `mixamorig:Hips` root
- Clips: `Idle` (3.0 s loop) and `Taunt` (about 2.83 s, non-looping)
- Material slots: 1

## Runtime texture

- File: `Textures/sensen_basecolor_1024.png`
- SHA-256: `7038adabd34629373eef50c3740c3fc3a21371d09aad4190bd6df4f3e032a770`
- Size: 1024 x 1024
- Color space: sRGB
- Material: Built-in Render Pipeline `Standard`

The original 2,000,000-triangle FBX and original 8192 x 8192 packed texture are
not stored in product Assets. The 80k performance fallback and 150k visual
reference remain in the repository-external local art workspace.

## Reproduction

- Blender: 5.2.0 LTS arm64
- Blender plug-ins: none
- Processing script: `tools/art/r30_build_candidate.py`
- Product import builder: `Assets/Editor/SensenRiggedAssetBuilder.cs`

The source FBX must be supplied separately when running the Blender processing
script. The script does not download or discover replacement models.

## Known limitation

The source skeleton has no tail bones. Tail skeletal animation is intentionally
outside R3.0 and remains a non-blocking art limitation.

# KOHAMI Editorial Model 01

Private visual reference pack for the fictional adult model used in KOHAMI's apparel catalog.

## Files

- `references/01-reference-editorial-home.png` — secondary editorial and outfit reference.
- `references/02-reference-catalog-look.png` — secondary catalog composition reference.
- `references/03-selected-canvas.png` — primary visual direction from the third canvas selected by the user.
- `../../CHARACTER_PROFILE.yaml` — stable identity traits and reference hashes.
- `../../STYLE_PROFILE.yaml` — reusable lighting, composition and garment-fidelity rules.
- `../../CHARACTER_ARTWORK_MANIFEST.yaml` — provenance records for generated and reviewed outputs.

The three PNGs are original full-page screenshots supplied by the user (1435–1436 × 1096). They are reference material, not clean identity portraits or approved training data. The screenshots contain webpage text and multiple depictions; do not pass them directly as isolated face references. The first approved identity master remains pending local generation and human review.

## Local generation gate

The user-selected target is local `flux3-image-klein` using the supported `mflux-generate-flux3` runtime. The installed AIPS executor and MFLUX 0.22.0 do not currently support this model, so generation is blocked until a supported adapter/runtime and local prompt upsampler pass preflight. Do not use a cloud image service or bypass the project creative executor.

See [project image-generation policy](../../../../docs/agents/model-image-generation.md).

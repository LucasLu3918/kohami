# KOHAMI Editorial Model 01

Private visual reference pack for the fictional adult model used in KOHAMI's apparel catalog.

## Files

- `references/01-reference-editorial-home.png` — secondary editorial and outfit reference.
- `references/02-reference-catalog-look.png` — secondary catalog composition reference.
- `references/03-selected-canvas.png` — primary visual direction from the third canvas selected by the user.
- `references/04-identity-master-selected.png` — user-selected third generated character image and sole identity anchor.
- `references/05-expression-window-smile.png` — local soft-smile and window-light reference.
- `references/06-pose-seated.png` — local seated-pose reference.
- `references/07-pose-walking.png` — local walking and full-body reference.
- `references/08-saori-candidate-b-supplemental.png` — user-selected park, full-body pose, and expression reference; not an Iori face source.
- `references/09-iori-three-angle-supplemental.png` — three-angle supplement mapped to Iori from the source run's identity input; recovered image hash differs from the output manifest.
- `IDENTITY_GUIDE.md` — stable character traits, image-use rules, local generation constraints, and review criteria.
- `PROMPT_LIBRARY.yaml` — exact local prompts, reusable prompt templates, hashes, seeds, and per-run provenance.
- `generation-runs/2026-10-24/` — exact profile, style, workflow, and Bundle snapshots used to generate the three supplemental images.
- `generation-runs/2026-10-25/qwen-image-2.1-edit/` — three image-conditioned outfit concepts, exact prompts, AIPS manifests, and corrected photographic style profile.
- `profiles/flux3-image-klein-distilled-int8.yaml` — optional fixed local FLUX 3 Klein distilled INT8 profile; preflight-ready, inference not yet verified.
- `workflows/flux3-image-klein-distilled-api.json` — fixed ComfyUI API workflow used by that profile.
- `profiles/qwen-image-2.1-edit-int8.yaml` — task-scoped local Qwen Image 2.1 profile; use only with the single-pose guard, a passing first-image pilot, and exact identity/product references. The user attested public-use authorization; the Qwen Research License remains separately recorded.
- `workflows/qwen-image-2.1-edit-api.json` — fixed local ComfyUI workflow; the selected master is passed as `image_1`.
- `profiles/qwen-image-edit-2511-dual-reference.yaml` — local dual-reference profile; preflight passed, but inference is blocked by memory pressure on the current Apple MPS host.
- `workflows/qwen-image-edit-2511-dual-reference-api.json` — AIPS-supported dynamic ComfyUI workflow with identity and product in separate ordered inputs.
- `workflows/garment-palette-remap-ui.json` — ComfyUI Desktop post-generation garment recolor workflow. Requires the separately installed `GarmentPaletteRemap` custom node; supply a same-size aligned garment-only mask (white selects recolor, black preserves). This UI workflow is manual and is not an AIPS API generation workflow.
- `../../CHARACTER_PROFILE.yaml` — stable identity traits and reference hashes.
- `../../STYLE_PROFILE.yaml` — reusable lighting, composition and garment-fidelity rules.
- `../../CHARACTER_ARTWORK_MANIFEST.yaml` — provenance records for generated and reviewed outputs.

The original user-supplied reference images remain unchanged. The selected identity master is a prior generated image selected by the user; its original model, seed, and workflow metadata were not retained. The three 2026-10-24 Z-Image-Turbo supplemental images remain text-only pose and mood references. The three 2026-10-25 Qwen Image 2.1 outputs used the master as an edit reference and produced photo-style outfits with standing, walking, and seated poses; they are concept tests accepted by the user on 2026-10-25 as workflow baselines, and do not guarantee exact future identity or product fidelity.

## Local generation gate

The active target remains local Tongyi-MAI `z-image-turbo` through AIPS 0.83.0's fixed ComfyUI profile. This text-to-image workflow has no identity-image or product-image input, so it cannot claim identity lock or product fidelity. The opt-in local `qwen-image-2.1-edit-int8` profile now accepts the user-selected identity master and passed local schema, weight, staged-reference hash, and inference checks; use it as the primary workflow for this character's outfit generation, always with the selected master as image_1 and a fresh edit from that master for each image. Z-Image-Turbo remains the general project default. Do not switch models automatically or use a cloud fallback.

The Qwen Image 2.1 profile uses the Qwen Research License. Its generated concepts are internal-only until commercial eligibility is reviewed. The user accepted its three image-conditioned samples as workflow baselines on 2026-10-25. That small sample is not an identity guarantee. Use a supplied product image when garment accuracy matters; the three tests used text-described outfits and are not exact SKU photography.

An additional FLUX candidate remains explicitly opt-in at `profiles/flux3-image-klein-distilled-int8.yaml`; it is not active. Its prior long-running prompt upsampler and visual review history are documented in `docs/agents/model-image-generation.md`. Commercial eligibility still requires review before any store use.

The KOHAMI source profiles use `schema_version: 1`; AIPS Creative workspace profiles expect `version: 1`. If copying the profiles into an ephemeral AIPS workspace, adapt only that top-level key in the workspace copy and preserve the project originals. AIPS Creative execution is restricted to a non-Git EPHEMERAL workspace.

See [project image-generation policy](../../../../docs/agents/model-image-generation.md).

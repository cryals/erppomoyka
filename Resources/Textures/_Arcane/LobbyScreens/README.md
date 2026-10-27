# Arcane lobby backgrounds

PNG, JPG/JPEG, WebP and GIF are served through the local WebUI resource host.
Register images using `lobbyBackground` prototypes; existing IDs remain stable.
Animated images should declare a static `poster` for native fallback and reduced motion.

The six inherited WebP backgrounds were relocated from `_Trauma/LobbyScreens`
without image modifications. Their existing prototype titles/authors and sampling metadata are retained.

`arcane.jpg`, `space.png`, `spacex.webp` and `rat.gif` were supplied by the project
owner on 2026-10-27. Their original `.license.txt` documents are preserved verbatim.
`rat-poster.png` is the first frame of `rat.gif`; it has the same author and permission.
The GIF permission is specific to Arcane, non-transferable, and does not grant
downstream projects a license. Do not copy these assets into the reusable WebUI submodule.

Background titles are descriptive UI labels, not claims about original artwork titles.
The SpaceX example's supplied notice does not name a photographer; the UI identifies
its source as Pexels instead of inventing an author.

# Mod icons

Ship the logo inside your DLL by adding an embedded resource to your project:

```xml
<EmbeddedResource Include="assets/icon.png" LogicalName="ExampleMod.Icon.png" />
```

Supply `Icon: ModIcon.FromResource(typeof(Plugin).Assembly, "ExampleMod.Icon.png")`
in `ModMetadata`, or `Icon: ModIcon.FromBytes(pngBytes)` if you already have encoded
PNG/JPG data. Companion copies the bytes, decodes once when displaying the menu,
and reuses the sprite for the top tab and browser row. No extraction or loose file
is required. Supply either `Icon` or `IconPath`; providing both is rejected.
Missing embedded resources fail during registration with a descriptive error;
undecodable image data logs a warning and uses the initials fallback.

Prefer a small square PNG with a transparent background and a simple shape readable at tab size. Images preserve their aspect ratio. Initials are used when no image is supplied. Name, author and version are required independently of the icon. Optional `IconPath` is an absolute local PNG/JPG path; embedded resources avoid distributing loose files.

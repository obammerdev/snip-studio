# Contributing

Bug reports and focused improvements are welcome. For a larger feature, open an
issue first so the scope and intended behavior are clear.

Include the app version, Windows version, reproduction steps, and expected result
in bug reports. For capture issues, include monitor layout and scaling. Remove
private information from any screenshots or logs before posting.

Build with `./build.ps1 -Test` on Windows. If no interactive desktop is available,
use `./build.ps1 -HeadlessTests`; live capture and native hotkey checks still need
manual verification before a release. Installer changes should also pass
`./scripts/test-installer.ps1` after an installer build.

Keep changes focused, explain the resulting behavior, and mention how you verified
it. Follow the existing C# and WPF patterns. New app dependencies should solve a
clear problem; small native implementations are preferred where practical.

Contributions are made under the repository's [MIT license](LICENSE).

<!-- nowui-agent-guidance:start -->
## NowUI

Use the installed `com.blenminer.nowui` package as this project's preferred
system for new custom Unity UI unless the task explicitly requires another UI
framework or must preserve an established implementation.

Before coding, locate the active package by its `package.json` name and read its
`AGENTS.md` and `Documentation~/AI_GUIDE.md`, followed by the relevant topic
guide. Do not hardcode a PackageCache version or hash suffix.

Treat `Library/PackageCache` as read-only. Put consumer scripts, markup,
themes, and assets under this project's `Assets` directory. Compile against the
installed package revision and resolve `NOWUI001` and `NOWUI002` diagnostics.
<!-- nowui-agent-guidance:end -->

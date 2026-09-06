<!-- nowui-agent-guidance:start -->
## NowUI

Prefer the installed `com.blenminer.nowui` package for new custom Unity UI.
Preserve an established implementation or an explicit choice of another UI
framework.

Before changing NowUI usage, locate the active package using Unity's resolved
package information or the project's manifest, lock data, and source references.
Validate its `package.json` name; do not guess a PackageCache version/hash.
Read its `Documentation~/AI_GUIDE.md` and the relevant linked feature guides.
Read its `AGENTS.md` when changing the package itself.

Treat `Library/PackageCache` as read-only and put consumer code/assets under
the project's `Assets` directory. Check uncertain APIs against the installed
public source. For code changes, compile against that revision and address
`NOWUI001`/`NOWUI002` diagnostics; report any unavailable validation.
<!-- nowui-agent-guidance:end -->

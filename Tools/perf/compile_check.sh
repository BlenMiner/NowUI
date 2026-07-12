#!/bin/bash
DAG="Library/Bee/artifacts/1900b0aE.dag"
ROSLYN="/c/Program Files/Unity/Hub/Editor/6000.4.0f1/Editor/Data/DotNetSdkRoslyn"
OUT=$(mktemp -d -p "$LOCALAPPDATA/Temp" nowui-cc-XXXXXX)
OUTWIN=$(cygpath -m "$OUT")
trap 'rm -rf "$OUT"' EXIT
FAIL=0
BUILT=""
for ASM in "$@"; do
  RSP="$DAG/$ASM.rsp"
  if [ ! -f "$RSP" ]; then echo "MISSING RSP: $RSP"; FAIL=1; continue; fi
  CLEAN="$OUT/$ASM.rsp"
  grep -v -e '^-analyzer' -e '^-additionalfile' -e '^-refout' "$RSP" | sed "s|^-out:.*|-out:\"$OUTWIN/$ASM.dll\"|" > "$CLEAN"
  for B in $BUILT; do
    sed -i "s|-r:\"$DAG/$B.ref.dll\"|-r:\"$OUTWIN/$B.dll\"|" "$CLEAN"
  done
  if dotnet exec "$ROSLYN/csc.dll" -noconfig "@$CLEAN" > "$OUT/$ASM.log" 2>&1; then
    echo "OK: $ASM"
    BUILT="$BUILT $ASM"
  else
    echo "FAIL: $ASM"
    grep -E "error" "$OUT/$ASM.log" | head -30
    FAIL=1
  fi
done
exit $FAIL

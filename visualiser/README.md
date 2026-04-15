# AR Guzheng Tiaozhan

## Setup

1. Install Git LFS

2. Setup Git LFS. 

```bash
git lfs install
# Output: 
# Updated git hooks.
# Git LFS initialized.
```

3. Clone the repo

4. Pull LFS files (If unity looks broken)

```bash
git lfs pull
```

Note:

- never delete `.gitattributes`
- if need to add more big files, use `git lfs track <file_extension>` before adding them to the repo.

```bash
# for example
git lfs track "*.psd"
```

## Resolving merge conflicts
Sometimes we might encounter merge conflicts in the scene or project files. Manually resolving these conflicts can be tricky. 

Edit `.gitattributes` to include the lines:

```bash
*.unity merge=unityyamlmerge
*.prefab merge=unityyamlmerge
```

Setup and run UnityYAMLMerge tool
```bash
# setup UnityYAMLMerge tool:
git config --global merge.tool unityyamlmerge
git config --global mergetool.unityyamlmerge.cmd "'/home/nicholas_tyy/Unity/Hub/Editor/2022.3.62f3/Editor/Data/Tools/UnityYAMLMerge' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global mergetool.unityyamlmerge.trustExitCode false
```

```bash
# using UnityYAMLMerge tool (already configured):
git mergetool
```

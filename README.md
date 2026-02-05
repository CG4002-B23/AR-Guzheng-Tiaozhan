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


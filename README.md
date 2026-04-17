# AR Guzheng Tiaozhan

This README covers the setup and replication steps for the project.

---

## Table of Contents

- [Visualiser Setup](#visualiser-setup)
- [Comms Setup](#comms-setup)
  - [1. Mosquitto Setup](#1-mosquitto-setup)
  - [2. Certificate Path Setup](#2-certificate-path-setup)
  - [3. mDNS Setup (Avahi)](#3-mdns-setup-avahi)
  - [4. Arduino Setup](#4-arduino-setup)
  - [5. Ultra96 Setup](#5-ultra96-setup)
- [More Resources](#more-resources)

---

## Visualiser Setup

The Visualiser is a Unity-based AR application managed with Git LFS.

### Prerequisites

- Git
- Git LFS
- Unity Hub with Unity Editor **2022.3.62f1**

### Steps

**1. Install Git LFS**

```bash
git lfs install
# Expected output:
# Updated git hooks.
# Git LFS initialized.
```

**2. Clone the repository**

```bash
git clone <repo-url>
```

**3. Pull LFS files** (run this if Unity assets appear broken or missing after cloning)

```bash
git lfs pull
```

### Git LFS Notes

- **Never delete** `.gitattributes` — it tracks which file types are managed by LFS.
- To track additional large file types, run the following **before** adding them to the repo:

```bash
git lfs track "*.psd"
# Replace *.psd with the relevant file extension
```

### Resolving Unity Merge Conflicts

Unity scene (`.unity`) and prefab (`.prefab`) files can cause merge conflicts that are difficult to resolve manually. Use the **UnityYAMLMerge** tool instead.

**Step 1:** Add the following lines to `.gitattributes`:

```
*.unity merge=unityyamlmerge
*.prefab merge=unityyamlmerge
```

**Step 2:** Configure the merge tool (one-time setup):

```bash
git config --global merge.tool unityyamlmerge
git config --global mergetool.unityyamlmerge.cmd "'/home/nicholas_tyy/Unity/Hub/Editor/2022.3.62f3/Editor/Data/Tools/UnityYAMLMerge' merge -p \"\$BASE\" \"\$REMOTE\" \"\$LOCAL\" \"\$MERGED\""
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global mergetool.unityyamlmerge.trustExitCode false
```

> **Note:** Update the path above to match your local Unity installation if it differs.

**Step 3:** Run the merge tool when a conflict occurs:

```bash
git mergetool
```

### APK File
Access the APK for the visualiser [here](https://drive.google.com/file/d/1QvoHVhFhJa4knbPaPK581uXEcmhk6ccR/view?usp=sharing).

---

## Comms Setup

The Comms component handles MQTT-based communication between the laptop, Arduino, and Ultra96 FPGA board over a TLS-secured connection.

### 1. Mosquitto Setup

Create the certificates directory:

```bash
mkdir /etc/mosquitto/certs
```

Copy `broker.crt`, `broker.key`, and `ca.crt` into `/etc/mosquitto/certs/`.

Set the correct ownership for the broker key:

```bash
sudo chown mosquitto:mosquitto /etc/mosquitto/certs/broker.key
```

Open the Mosquitto config file:

```bash
sudo vim /etc/mosquitto/mosquitto.conf
```

Replace the contents with the following:

```conf
persistence true
persistence_location /var/lib/mosquitto/
log_dest file /var/log/mosquitto/mosquitto.log

#include_dir /etc/mosquitto/conf.d

# TLS Listener (port 8883)
listener 8883 0.0.0.0

# Certificate paths
cafile /etc/mosquitto/certs/ca.crt
certfile /etc/mosquitto/certs/broker.crt
keyfile /etc/mosquitto/certs/broker.key

# TLS settings
tls_version tlsv1.2
require_certificate true
allow_anonymous true
```

---

### 2. Certificate Path Setup

In the **laptop broker code**, update the certificate file paths to match your local setup:

```python
CA_CERT     = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/ca/ca.crt"
CLIENT_CERT = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.crt"
CLIENT_KEY  = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.key"
```

---

### 3. mDNS Setup (Avahi)

mDNS is required so the Arduino can resolve the laptop's hostname on the local network.

```bash
sudo apt install avahi-daemon
sudo systemctl enable avahi-daemon
sudo systemctl start avahi-daemon
```

---

### 4. Arduino Setup

- Ensure all `config.h` files are present in the Arduino project.
- Verify that the certificate contents in `config.h` match the certificates in the `mqtt_certs` folder.
- In the main Arduino sketch, update the `ssid` and `password` fields to match your network credentials.

> **Note:** If using a portable router, the fixed IP address must use the same subnet as the one assigned to your laptop. Run the following to check your laptop's IP:
>
> ```bash
> ifconfig
> ```

---

### 5. Ultra96 Setup

The `ultra96_mqtt` folder contents should already be present on the Ultra96 at `b23/test_mqtt`.

#### Setting Up the SSH Tunnel

In a terminal, run the following to forward port 8883 from your laptop to the Ultra96:

```bash
ssh -R 8883:localhost:8883 xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

A successful connection will display:

```
xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg's password:
Welcome to PYNQ Linux, based on Ubuntu 22.04 (GNU/Linux 5.15.19-xilinx-v2022.1 aarch64)
```

> **Important:** Leave this terminal tab open. Open a **separate terminal tab** to SSH into the Ultra96 for subsequent commands.

#### If There Is an Error Listening on Port 8883

SSH into the Ultra96:

```bash
ssh xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

Find the process occupying port 8883:

```bash
sudo lsof -i :8883
```

Kill the process using the task ID from the output above:

```bash
sudo kill -9 <task_id>
```

Then re-run the SSH tunnel command in a new terminal tab:

```bash
ssh -R 8883:localhost:8883 xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

#### Starting the Ultra96 MQTT Client

```bash
sudo su
./run_mqtt
```

## More Resources
Click [here](https://drive.google.com/file/d/1QvoHVhFhJa4knbPaPK581uXEcmhk6ccR/view?usp=sharing) to download the APK for the visualiser, and [here](https://docs.google.com/document/d/1jq-NAarq0MVdrMZtwGHCQtAdrvzGgn8LGwj4qw9SXLc/edit?usp=sharing) to view the final report, and [here](https://youtu.be/2PQ7bNgT9PA?si=lnkjrBgCULc_K95v) to view a demo of our project, and [here](https://canva.link/wifwy825md1w9br) to view the poster of our project.

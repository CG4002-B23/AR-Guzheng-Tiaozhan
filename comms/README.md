# Running Comms

## 1. Mosquitto Setup

Create the certificates directory and copy the required certificates into it:

```bash
mkdir /etc/mosquitto/certs
```

Navigate into the `certs` folder and copy `broker.crt`, `broker.key`, and `ca.crt` into it.

Set the correct ownership for the broker key:

```bash
sudo chown mosquitto:mosquitto /etc/mosquitto/certs/broker.key
```

Open and edit the Mosquitto config file:

```bash
sudo vim /etc/mosquitto/mosquitto.conf
```

Paste the following configuration:

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

## 2. Certificate Path Setup

In the **laptop broker code**, update the file paths to point to where your certificates are located:

```python
CA_CERT     = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/ca/ca.crt"
CLIENT_CERT = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.crt"
CLIENT_KEY  = "/home/eugene/Desktop/CG4002/Working_Code/mqtt_certs/client/laptop_broker.key"
```

---

## 3. mDNS Setup (Avahi)

mDNS is required for the Arduino to resolve the laptop's hostname on the local network. Install and enable the Avahi daemon with the following commands:

```bash
sudo apt install avahi-daemon
sudo systemctl enable avahi-daemon
sudo systemctl start avahi-daemon
```

---

## 4. Arduino Setup

- Ensure all `config.h` files are present and that the certificate contents match those found in the `mqtt_certs` folder.
- In the main Arduino code, update the `ssid` and `password` fields to match your network credentials.

> **Note:** If you are using a portable router, the fixed IP must use the same address format as what your laptop is assigned. Run the following to check:
> ```bash
> ifconfig
> ```

---

## 5. Ultra96 Setup

The contents of `ultra96_mqtt` should already be present on the Ultra96 at `b23/test_mqtt`.

### Setting Up the SSH Tunnel

In a terminal, run the following to forward port 8883:

```bash
ssh -R 8883:localhost:8883 xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

A successful connection should look like this:

```
xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg's password:
Welcome to PYNQ Linux, based on Ubuntu 22.04 (GNU/Linux 5.15.19-xilinx-v2022.1 aarch64)
```

> **Important:** Leave this terminal tab open. Open a **separate terminal tab** to SSH into the Ultra96.

### If There Is an Error Listening on Port 8883

SSH into the Ultra96:

```bash
ssh xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

Find the process using port 8883:

```bash
sudo lsof -i :8883
```

Kill the process using the task ID found above:

```bash
sudo kill -9 <task_id>
```

Then rerun the SSH tunnel command in a new terminal tab:

```bash
ssh -R 8883:localhost:8883 xilinx@makerslab-fpga-30.ddns.comp.nus.edu.sg
```

### Starting the Ultra96 MQTT Client

```bash
sudo su
./run_mqtt
```

# Sprint 1-9

## Device Validation

Status

Planned

---

## Goal

Improve Add/Edit Device validation.

Prevent invalid data from being saved.

Validation is performed before calling DeviceService.

---

## Scope

### Required Fields

- Name
- IP Address
- Username
- Connection Type

---

### IP Validation

Support IPv4 only.

Example:

192.168.1.10

Reject:

999.999.999.999

abc

192.168.1

---

### Port Validation

Validate:

- HTTP Port
- SDK Port
- RTSP Port

Range:

1 ~ 65535

Reject:

0

70000

text

---

### RTSP URL

If Brand = RTSP

RTSP Url is required.

Must begin with

rtsp://

---

### Save Validation

Save button performs validation.

If validation fails:

- show MessageBox
- do not call DeviceService

---

## Files

Modify

- AddDeviceWindow.xaml
- AddDeviceWindow.xaml.cs

Do NOT modify

- MainWindow
- Repository
- SQLite
- Driver
- DeviceService

---

## Acceptance

✓ Required fields checked

✓ IPv4 validation

✓ Port validation

✓ RTSP URL validation

✓ Save blocked when invalid

✓ Existing Add/Edit reused

✓ Build Success

✓ 0 Error
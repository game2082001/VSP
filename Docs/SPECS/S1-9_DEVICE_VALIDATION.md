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

# Sprint 1-9

## Device Validation

Status

Completed

---

## Goal

Validate Add/Edit Device input before saving.

Prevent invalid data from reaching DeviceService.

---

## Scope

Completed

### Required Fields

- Name
- IP Address
- Username
- Connection Type

### IPv4 Validation

Support IPv4 only.

Reject invalid IP address.

### Port Validation

Validate:

- HTTP Port
- SDK Port
- RTSP Port

Range:

1 ~ 65535

### RTSP Validation

When Brand = RTSP

- RTSP URL is required.
- Must start with:

rtsp://

### Save Validation

Validation is performed before calling DeviceService.

Validation failure:

- MessageBox
- Focus invalid control
- Cancel Save

---

## Files Changed

- AddDeviceWindow.xaml.cs

---

## Files NOT Changed

- MainWindow
- Repository
- SQLite Schema
- Driver Framework
- DeviceService

---

## Validation Flow

User Click Save

↓

Required Fields

↓

IPv4 Validation

↓

Port Validation

↓

RTSP Validation

↓

Call DeviceService

↓

Save SQLite

---

## Acceptance

✓ Required field validation

✓ IPv4 validation

✓ Port validation

✓ RTSP URL validation

✓ Add/Edit share same validation

✓ Validation before DeviceService

✓ Build Success

✓ 0 Errors

---

Status

Completed
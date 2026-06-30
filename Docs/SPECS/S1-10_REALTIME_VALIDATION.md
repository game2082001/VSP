# Sprint 1-10

## Real-time Validation

Status

Planned

---

## Goal

Improve Add/Edit Device user experience.

Validate user input immediately while typing.

Prevent users from reaching Save with invalid data.

---

## Scope

### IPv4

While typing:

- Invalid IP → Red Border
- Valid IP → Normal Border

---

### Port

Validate immediately:

- HTTP Port
- SDK Port
- RTSP Port

Range

1~65535

Invalid

- Red Border

---

### Required Fields

Validate:

- Name
- IP Address
- Username
- Connection Type

Empty

↓

Red Border

---

### RTSP URL

When Brand = RTSP

RTSP URL becomes Required.

Must begin with

rtsp://

Otherwise

↓

Red Border

---

### Save Button

When any validation fails

↓

Disable Save Button

When all valid

↓

Enable Save Button

---

### Error Message

Show below textbox

Example

Invalid IPv4 address.

Port must be between 1 and 65535.

RTSP URL must begin with rtsp://

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

✓ Immediate IPv4 validation

✓ Immediate Port validation

✓ Required field validation

✓ RTSP validation

✓ Save disabled when invalid

✓ Save enabled when valid

✓ Build Success

✓ 0 Error

## Result

Status

Completed

Implemented

- Realtime validation while typing.
- Save button enable / disable by validation state.
- Error messages below invalid controls.
- Invalid controls highlighted with red border.
- Existing S1-9 save validation retained.
- No Repository / SQLite / DeviceService changes.

Build

Build Success

Error

0

Warning

7 (existing SQLitePCLRaw warnings)
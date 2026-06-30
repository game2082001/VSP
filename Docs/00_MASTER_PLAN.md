# VSP Master Plan

Version: 1.0

Last Updated: 2026-06-30

---

# 1. Project Overview

Project Name

VSP (Video Surveillance Platform)

Purpose

VSP 是一套商業級 Video Management System (VMS)。

本系統目標不是單純管理攝影機，而是建立一套可擴充的智慧安防平台。

未來可整合：

- IP Camera
- NVR
- Door Access Controller
- AI Box
- Decoder
- Display Wall
- IoT Device
- CMS
- Alarm System

---

# 2. Product Vision

建立一套：

- 高效能
- 高穩定
- 可維護
- 可擴充
- 多品牌支援
- 多站點管理

之商業級 VMS。

---

# 3. Core Principles

Architecture First

Documentation Driven

Reuse Before Rewrite

Quality Over Speed

No Technical Debt

---

# 4. System Architecture

Presentation Layer

↓

MVVM

↓

Application Layer

↓

Service Layer

↓

Repository Layer

↓

SQLite

↓

Driver Framework

↓

Vendor SDK / Protocol

---

# 5. Core Modules

Dashboard

Device Center

Live View

Playback

Event Center

Alarm Center

AI Center

Driver Center

Storage Manager

User Manager

Health Monitor

Settings

CMS

---

# 6. Device Model

VSP 不以 Camera 為核心。

VSP 以 Device 為核心。

Device

├── Camera

├── NVR

├── Door Controller

├── AI Box

├── Decoder

├── Display

├── IoT Device

└── Future Device

Camera 只是 Device 的一種類型。

---

# 7. Driver Architecture

所有設備皆透過 Driver Framework 存取。

View

↓

ViewModel

↓

Service

↓

DriverFactory

↓

IDeviceDriver

↓

Vendor Driver

↓

SDK / Protocol

不得：

ViewModel → SDK

Repository → Driver

View → SDK

---

# 8. Database

SQLite

Repository Pattern

所有 SQL 僅能存在 Repository。

不得：

ViewModel

↓

SQLite

---

# 9. Development Principles

每個 Sprint 必須包含：

Spec

↓

Task Plan

↓

Architecture Review

↓

Coding

↓

Build

↓

Code Review

↓

Testing

↓

Documentation

↓

Git Commit

---

# 10. Product Roadmap

V0.1

Device Center

V0.2

Live View

V0.3

Playback

V0.4

Event Center

V0.5

Driver Framework

V0.6

System Settings

V0.7

User Management

V0.8

AI Integration

V0.9

Enterprise Features

V1.0

Official Release

---

# 11. Quality Goals

Every Build

- Build Success
- 0 Error
- Warning Reviewed

Every Sprint

- Documentation Updated
- Spec Completed
- Git Commit
- Code Review Passed

---

# 12. Long-term Vision

VSP 將持續發展為：

Video Surveillance Platform

+

Access Control

+

AI Analytics

+

IoT Integration

+

Factory Security

+

Multi-site CMS

+

Enterprise Management

而非單純 NVR 管理軟體。

---

# Revision History

| Version | Date | Summary |
|----------|------------|------------------------------|
| 1.0 | 2026-06-30 | 建立 VSP Master Plan |
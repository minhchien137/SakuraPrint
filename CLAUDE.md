# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run (development)

dotnet run --project PrintApp

# Publish (Windows x64 release)
dotnet publish PrintApp -c Release -r win-x64 --self-contained
```

There are no automated tests in this project.

## Architecture

ASP.NET Core 8 MVC app (`RootNamespace: ZplPrinter`) that generates ZPL label data and coordinates printing to Zebra printers. The app is served under the path base `/print`.

### Print flow

The server never talks directly to a printer. Instead:
1. The browser calls a server API to get a ZPL string.
2. The browser sends that ZPL to `http://localhost:8021/print` — a local bridge agent running on the operator's machine.
3. The bridge forwards the ZPL over TCP/IP (port 9100) or USB to the Zebra printer.

This means printing only works from a machine running the local bridge.

### Three feature areas

**Generic ZPL print** (`PrintController` + `ZplService`):  
`POST /Print/GenerateZpl` accepts text content and label dimensions, builds a ZPL string with 203 DPI calculations and optional Code128 barcode (auto-added for single short values). The browser then drives the print.

**Toast box/pallet labels** (`ToastController` + `ToastService`):  
Label content comes from ZPL templates stored in the `SVN_Printer_Info_New` DB table (`PrinterInfo.ZPL_Temp`). `BuildToastZpl` / `BuildPalletZplAsync` do string-replace substitution on tokens like `{toastPartNumber}`, `{serialBlock1}`, `{lotId1}`, etc. Up to 5 serial numbers per box label; pallet labels aggregate serials from `SVN_Astro_Label_Data`. Serial validation checks the DB before printing to prevent duplicate scans. The Toast index page (`/toast`) stores form state in `localStorage` under key `toast_form_v1`.

**FCT/FQC serial tracking** (`ToastSerialController` + `ToastSerialService`):  
Two-step QC workflow. `POST /FctScanToast/Submit` creates a new row in `SVN_Toast_Serial_Info` (FCT pass/fail). `POST /UpdateFqcStatus` updates the FQC status on an existing record — blocked if FCT was NG or FQC already set. All timestamps are stored in Vietnam time (ICT/UTC+7), with OS-portable timezone resolution.

### Database

SQL Server at `10.10.99.10`, database `svn_pentaho`. Three tables mapped via EF Core (no migrations — schema is pre-existing):

| Entity | Table |
|---|---|
| `AstroLabelData` | `SVN_Astro_Label_Data` |
| `PrinterInfo` | `SVN_Printer_Info_New` |
| `SVNToastSerialInfo` | `SVN_Toast_Serial_Info` |

`PrinterInfo.target` filters printers by use-case (`"Toast"` for Toast printers). The pallet printer is looked up by `Name_Printer == "Pallet_Toast"` and the FQC printer by `Name_Printer == "TEST_TOAST_1SERIAL"`.

### SKU metadata

`ToastService.ResolveSkuMeta` (and a JS mirror `resolveSku` in `Toast/Index.cshtml`) maps SKU codes to product description strings. Both must be kept in sync when SKUs are added.

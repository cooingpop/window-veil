# Window Veil

**Cover selected Windows app windows when you are not using them.** Window Veil lives in the system tray and places a cover over a selected window when another window is active. Return to the app to uncover it, or hold **Ctrl** while hovering over the cover for a quick peek.

Window Veil is a visual privacy aid for people nearby. It does not lock an app, encrypt its contents, or stop screenshots and screen sharing.

## What it does

- Follows selected app windows, including related windows, without taking keyboard focus.
- Keeps the original app in the taskbar and Alt+Tab.
- Offers blur, blank terminal, simulated terminal logs, blank Notepad, blank spreadsheet, or your own image as a cover.
- Lets you leave selected parts of a window visible, such as a message input box.
- Supports monitors with different display scaling settings.
- For apps that use Windows notifications, such as Discord, shows a content-free "new notification" signal instead of the app's banner. Windows draws notification banners above every window, so they cannot be covered; you turn that app's banners off in Windows Settings and Window Veil tells you only that something arrived.

The built-in terminal, Notepad, and spreadsheet covers contain sample content. You can demonstrate the app with those covers without showing your own conversations or documents.

## Download and run

Download from [Releases](https://github.com/cooingpop/window-veil/releases). Windows 11 and its included .NET Framework are the intended environment. No separate PowerShell window or compile step is needed for release builds.

| Download | Best for | How to start |
|---|---|---|
| `WindowVeil-Setup-…-win-x64.exe` | A normal per-user installation | Run the installer, then launch **Window Veil** from the Start menu. |
| `WindowVeil-…-win-x64.zip` | Running without an installer | Extract the ZIP and run `WindowVeil.exe`. |

Both editions save settings under `%APPDATA%\WindowVeil`; moving the ZIP does **not** move your settings. Only one instance runs at a time. Quit from the tray menu.

## Use

1. Open the shield icon in the system tray.
2. Choose **가릴 프로그램 고르기** (“Choose apps to cover”) and select the programs you want to cover.
3. Under **가린 창 꾸미기** (“Customize covered windows”), choose a cover style or set an area that should remain visible.
4. Hover over a covered window and hold **Ctrl** to peek. Activate the original window to remove its cover.
5. To hide notification content, open **가린 창 꾸미기** → the app → **알림이 오면 내용 없이 알려주기** ("Notify me without content"). A guide opens with a button for Windows notification settings; turn off **Show notification banners** for that app there. When a notification arrives, Window Veil shows only "<app> 새 알림" ("new notification") at the bottom right, counts repeats, and switches to the app when clicked. It stays quiet while you are using that app. The notification itself remains in the Windows notification center.

The interface is currently in Korean. The tray menu also contains a help view (**사용법 보기**), a temporary pause option, and Quit.

## Build from source

On Windows with the .NET Framework C# compiler, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This produces `dist\WindowVeil.exe`. The installer is built with [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
$version = (Get-Content VERSION).Trim()
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DAppVersion=$version" installer\WindowVeil.iss
```

For source-only development, `run-hidden.vbs` still starts `run.ps1`, which compiles `src\Veil.cs` in PowerShell at each launch. Release builds use the compiled executable.

## Versioning

The current version is in [`VERSION`](VERSION). Releases follow `MAJOR.MINOR.PATCH`:

- **MAJOR**: a change that breaks existing behavior or compatibility.
- **MINOR**: a new feature that keeps existing behavior compatible.
- **PATCH**: a compatible bug fix or small correction.

Update `VERSION` in a pull request, then tag that commit as `vMAJOR.MINOR.PATCH`. The release workflow checks that the tag matches `VERSION`, embeds the version in `WindowVeil.exe`, and uses it for the installer and ZIP names. Published tags and release files are kept as historical versions.

## Local data and privacy

Window Veil runs locally. It stores selected executable names, cover preferences, optional image paths, content-free notification preferences, visible-area geometry, and a diagnostic log in `%APPDATA%\WindowVeil`. It does not save window titles or window contents. For the content-free notification signal it reads only the time Windows records for an app's latest notification (`LastNotificationAddedTime` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings`); it never reads notification text and never changes Windows notification settings. If you choose a custom image, the path to that file is stored. The log may contain app names and diagnostic details, so inspect it before sharing it in an issue.

| File | Purpose |
|---|---|
| `targets.txt` | Selected app executable names, cover styles, and whether the content-free notification signal is on |
| `areas.txt` | Visible-area positions and window-type markers |
| `veil.log` | Diagnostic events |

Uninstalling the app does not remove this user data. To remove your preferences too, quit Window Veil and delete `%APPDATA%\WindowVeil`.

## Current limitations

- The cover also hides the window from **you** until you activate it or peek. A visible area is visible to everyone looking at the screen.
- Screen sharing captures what is on your screen, including the cover. Window Veil is not protection against someone controlling your PC or accessing the original app directly.
- Manually checked so far: KakaoTalk PC, Discord, and Whale on Windows 11, including a 125% system scale and a 150% monitor.
- Windows notification banners cannot be covered; use the content-free notification option and turn the app's banners off in Windows Settings. Anyone who opens the Windows notification center can still read the notifications there.
- The content-free notification signal works only for apps that send Windows notifications. Apps that draw their own pop-up windows are covered like any other window of that app.
- Windows 10, Microsoft Store apps, always-on-top windows, and dragging a window between monitors with different scaling have not yet been verified. For the content-free notification signal, test notifications confirmed that Windows keeps updating an app's latest-notification time while its banners are off; the signal itself has not yet been tried with a real Discord message.

## Contributing

Please [open an issue](https://github.com/cooingpop/window-veil/issues/new/choose) for a bug or an idea. Include your Windows version, target app, monitor scaling, and cover style when reporting display problems. Remove personal text, names, email addresses, and file paths from screenshots and logs before posting. Pull requests are welcome; discuss larger changes in an issue first.

## License

[MIT](LICENSE).

---

### 한국어 안내

Window Veil은 다른 창을 사용하는 동안 선택한 프로그램 창을 가려 주는 Windows 트레이 앱입니다. [Releases](https://github.com/cooingpop/window-veil/releases)에서 설치 파일을 받거나 ZIP을 풀고 `WindowVeil.exe`를 실행하세요. 방패 아이콘의 **가릴 프로그램 고르기**에서 대상을 고르고, **가린 창 꾸미기**에서 가림 모양과 보이게 둘 영역을 설정할 수 있습니다. 가림 위에 마우스를 올린 채 **Ctrl**을 누르면 잠깐 볼 수 있습니다. Discord처럼 Windows 알림을 쓰는 앱은 알림 배너가 모든 창 위에 그려져 가릴 수 없으므로, **가린 창 꾸미기**에서 **알림이 오면 내용 없이 알려주기**를 켜고 안내에 따라 Windows 설정에서 그 앱의 알림 배너를 끄세요. 그러면 알림이 올 때 "Discord 새 알림"처럼 내용 없이 알려 줍니다. 설정은 설치형과 ZIP형 모두 `%APPDATA%\WindowVeil`에 저장됩니다.

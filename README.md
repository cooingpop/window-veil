# Window Veil

[한국어 안내](README.ko.md)

Window Veil is a small Windows tray app that covers the windows of apps you choose whenever you are not using them. People walking past your desk see a cover instead of your chats, mail, or documents. Click the window, or switch to it, and the cover goes away.

It is a visual privacy aid for people nearby. It does not lock apps, encrypt anything, or stop screenshots and screen sharing.

## Features

- **Covers the apps you choose** while another window is active. Covers follow the window when it moves or resizes, and windows in front of it are never covered.
- **Peek** at a covered window by hovering over it and holding **Ctrl**.
- **Cover styles**: blur, a blank terminal, a terminal with scrolling logs, blank Notepad, a blank spreadsheet, or your own image. The built-in styles contain sample content only, so they make it hard to tell that anything is covered.
- **Visible areas** keep part of a window uncovered, such as a message input box.
- **Notification signal** shows only "*app*: new notification" when a covered app sends a Windows notification, so you know something arrived without showing its content.
- **Start with Windows**: check it in the menu and Window Veil starts each time you sign in. Uncheck it to stop.
- Works across monitors with different display scaling.
- Shows its menus in Korean or English, following your Windows display language.

## Install

Download the latest version from [Releases](https://github.com/cooingpop/window-veil/releases). It is made for Windows 11 and needs nothing else installed.

| Download | Use it when | How to start |
|---|---|---|
| `WindowVeil-Setup-…-win-x64.exe` | You want a normal installation | Run the installer, then open **Window Veil** from the Start menu. |
| `WindowVeil-…-win-x64.zip` | You do not want to install anything | Extract the ZIP and run `WindowVeil.exe`. |

Window Veil lives in the system tray as a blue window icon. If you do not see it, click **^** at the right end of the taskbar.

To have it start every time you turn on your PC, check **Start Window Veil when Windows starts** in its menu.

## Getting started

1. Click the blue window icon.
2. Under **Choose apps to cover**, check the apps you want to cover. The menu stays open, so you can check several in a row.
3. Under **Customize covered windows**, pick an app to change its cover style, adjust visible areas, or set its notifications.
4. To use a covered app, click its window or switch to it. To glance at it without switching, hover over it and hold **Ctrl**.

The menu also has **Help**, **Start Window Veil when Windows starts**, **Pause all covers**, and **Quit Window Veil**.

## Notifications

Windows draws notification banners above every other window, so Window Veil cannot cover them. It also cannot turn them off for you: Windows only accepts that change from its own Settings app.

What Window Veil does instead:

- When a covered app sends a Windows notification, it shows **"*app*: new notification"** with a count at the bottom right. Click it to open the app. Nothing is shown while you are using that app.
- To hide what a notification says, that app's banners must be off in the Windows Settings app. When you choose an app to cover and its banners are on, Window Veil opens a short guide by itself. The guide shows **Hide notification content: not set up** in red and turns green (**done**) the moment you turn the banners off.
- The same state appears in the menu, at the top and under **Customize covered windows** > the app. Click it to open the guide again.
- **If an app's banners are off, turning off its "new notification" signal or no longer covering it means its notifications stop appearing on screen at all**, because Window Veil cannot turn the Windows banners back on. When that happens, Window Veil opens a notice that shows how to turn the signal back on or turn the banners back on in Windows Settings. Quitting Window Veil has the same effect, so **Quit Window Veil** shows which apps will go silent and asks once more.

You only need to turn the banners off once per app on each PC. Windows keeps this setting even if you reinstall Window Veil, and your notifications stay in the Windows notification center.

Apps that draw their own pop-up windows instead of using Windows notifications, such as KakaoTalk, do not need this. Their pop-ups are covered like any other window of that app.

## Language

Window Veil shows Korean when your Windows display language is Korean, and English otherwise. To choose a language yourself, set the environment variable `WINDOWVEIL_LANG` to `en` or `ko` and restart Window Veil:

```powershell
setx WINDOWVEIL_LANG en
```

## Privacy

Window Veil runs entirely on your PC and sends nothing anywhere. It never saves window titles or screen contents. It keeps its settings in `%APPDATA%\WindowVeil`:

| File | What it holds |
|---|---|
| `targets.txt` | The apps you chose, their cover styles (with the image path if you chose an image), and which apps have the notification signal turned off |
| `areas.txt` | Positions of visible areas |
| `veil.log` | A diagnostic log. It can contain app names, so check it before attaching it to an issue. |

For the notification signal, Window Veil reads only two values that Windows keeps for each app under `HKCU\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings`: the time of its latest notification and whether its banners are off. It never reads notification text and never changes notification settings.

The only Windows setting Window Veil changes is starting with Windows. When you check it, Window Veil adds one `WindowVeil` entry to your account's startup list (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). Unchecking it removes the entry, and so does uninstalling with the installer.

Uninstalling does not delete these files. To remove them, quit Window Veil and delete the `%APPDATA%\WindowVeil` folder.

## Good to know

- **The cover hides the window from you too.** Window Veil is not a privacy screen filter. To see a covered window, peek with **Ctrl** or switch to it.
- **Anyone can read what is inside a visible area**, such as text you have typed into an input box.
- **Screen sharing shows the cover**, so people in a meeting see it too.
- **It is not a lock.** Anyone who uses your keyboard and mouse can still open the app.
- **The Windows notification center still has the full notifications.**

Tested on Windows 11 with KakaoTalk, Discord, and Whale, including a monitor at a different display scale. Not tested yet: Windows 10, Microsoft Store apps, always-on-top windows, and dragging a window between monitors with different scaling.

## Build from source

With the C# compiler that comes with Windows (.NET Framework 4.x):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

This creates `dist\WindowVeil.exe`. To build the installer, install [Inno Setup 6](https://jrsoftware.org/isinfo.php) and run:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DAppVersion=$((Get-Content VERSION).Trim())" installer\WindowVeil.iss
```

For quick experiments without building, `run-hidden.vbs` starts `run.ps1`, which compiles `src\Veil.cs` in PowerShell each time it starts.

The app icon `assets\WindowVeil.ico` is drawn by `tools\make-icon.ps1`. Change the drawing there and run it again instead of editing the icon by hand.

## Contributing

Please [open an issue](https://github.com/cooingpop/window-veil/issues/new/choose) for bugs and ideas. For display problems, include your Windows version, the app you covered, your monitor scaling, and the cover style. Remove personal text, names, email addresses, and file paths from screenshots and logs first. Pull requests are welcome; for larger changes, please open an issue first.

## License

[MIT](LICENSE)

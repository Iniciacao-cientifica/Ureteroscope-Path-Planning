# Meta Quest setup and testing

## Phone and Meta account

1. Install/update **Meta Horizon** on the phone.
2. Sign in with the headset owner's Meta account and pair the Quest.
3. Join or create a developer team in the Meta Horizon Developer Dashboard.
4. Verify the developer account with phone number or accepted payment method.
5. In Meta Horizon open the headset icon, select the paired Quest, open **Headset Settings > Developer Mode**, and enable it.

The phone itself does not need Android developer options and is not connected to Unity.

## Headset and computer

1. Restart the Quest.
2. Connect it using a USB-C data cable.
3. In the headset open Quick Settings > Settings > Developer and enable MTP notifications.
4. Accept **Allow USB debugging** and **Always allow from this computer**.
5. Install the official Oculus ADB driver on Windows.
6. Confirm the device using Unity's ADB:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe" devices
```

Expected status: `device`. `unauthorized` means the confirmation inside the headset is still pending.

## Test cycle

1. Build/install with `build_vr_case.cmd ... -Install`.
2. Verify launch, head tracking, both controllers, rays, menu, model grips, route animation, cases, and routes.
3. Run continuously for 15 minutes.
4. Record FPS, loading time, visual alignment, discomfort, and the exact control used before any failure.
5. Collect logs:

```powershell
$Adb = "C:\Program Files\Unity\Hub\Editor\6000.5.0f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
& $Adb logcat -s Unity
```

## Acceptance checklist

- No black screen or OpenXR initialization error.
- Anatomy, stones, and route remain aligned.
- One-hand move/rotate and two-hand scale work.
- Trigger activates world-menu buttons.
- Route animation can stop immediately.
- Case and route switching do not duplicate objects.
- No crash or severe discomfort in 15 minutes.
- Target performance is stable near the configured 72 Hz refresh rate.

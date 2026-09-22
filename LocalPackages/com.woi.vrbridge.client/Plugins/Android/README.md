# Android Integration

The host OfisISG project must already declare:

```xml
<uses-permission android:name="android.permission.INTERNET" />
```

For LAN `ws://` development, merge into the application node:

```xml
android:usesCleartextTraffic="true"
android:networkSecurityConfig="@xml/network_security_config_woi_bridge"
```

Copy or merge `Plugins/Android/res/xml/network_security_config_woi_bridge.xml` into the exported Gradle project.

Production Quest builds should prefer `wss://` and remove cleartext exceptions.

The Java plugin `com.woi.vrbridge.security.WoiCredentialStore` stores the device token alias `woi_vrbridge_device_token` using Android Keystore AES/GCM.

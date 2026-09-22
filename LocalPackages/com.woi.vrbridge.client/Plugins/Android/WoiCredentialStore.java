package com.woi.vrbridge.security;

import android.content.Context;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Android Keystore backed credential store for WOI VR Bridge device tokens.
 */
public final class WoiCredentialStore {
    private static final String ANDROID_KEYSTORE = "AndroidKeyStore";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int GCM_TAG_LENGTH = 128;

    private static WoiCredentialStore instance;
    private final Context appContext;

    private WoiCredentialStore(Context context) {
        this.appContext = context.getApplicationContext();
    }

    public static synchronized WoiCredentialStore getInstance(Context context) {
        if (instance == null) {
            instance = new WoiCredentialStore(context);
        }
        return instance;
    }

    public boolean has(String alias) {
        try {
            KeyStore keyStore = KeyStore.getInstance(ANDROID_KEYSTORE);
            keyStore.load(null);
            return keyStore.containsAlias(alias);
        } catch (Exception ex) {
            return false;
        }
    }

    public void store(String alias, String plaintext) {
        if (plaintext == null || plaintext.isEmpty()) {
            throw new IllegalArgumentException("plaintext required");
        }

        try {
            ensureKey(alias);
            SecretKey key = getSecretKey(alias);
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.ENCRYPT_MODE, key);
            byte[] iv = cipher.getIV();
            byte[] cipherBytes = cipher.doFinal(plaintext.getBytes(StandardCharsets.UTF_8));

            byte[] payload = new byte[iv.length + cipherBytes.length];
            System.arraycopy(iv, 0, payload, 0, iv.length);
            System.arraycopy(cipherBytes, 0, payload, iv.length, cipherBytes.length);

            appContext.getSharedPreferences("woi_vrbridge_secure", Context.MODE_PRIVATE)
                    .edit()
                    .putString(alias, Base64.encodeToString(payload, Base64.NO_WRAP))
                    .apply();
        } catch (Exception ex) {
            throw new RuntimeException("Failed to store credential", ex);
        }
    }

    public String read(String alias) {
        try {
            String encoded = appContext.getSharedPreferences("woi_vrbridge_secure", Context.MODE_PRIVATE)
                    .getString(alias, null);
            if (encoded == null) {
                return null;
            }

            byte[] payload = Base64.decode(encoded, Base64.NO_WRAP);
            if (payload.length <= 12) {
                return null;
            }

            byte[] iv = new byte[12];
            byte[] cipherBytes = new byte[payload.length - 12];
            System.arraycopy(payload, 0, iv, 0, 12);
            System.arraycopy(payload, 12, cipherBytes, 0, cipherBytes.length);

            SecretKey key = getSecretKey(alias);
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(GCM_TAG_LENGTH, iv));
            byte[] plain = cipher.doFinal(cipherBytes);
            return new String(plain, StandardCharsets.UTF_8);
        } catch (Exception ex) {
            return null;
        }
    }

    public void delete(String alias) {
        try {
            appContext.getSharedPreferences("woi_vrbridge_secure", Context.MODE_PRIVATE)
                    .edit()
                    .remove(alias)
                    .apply();

            KeyStore keyStore = KeyStore.getInstance(ANDROID_KEYSTORE);
            keyStore.load(null);
            if (keyStore.containsAlias(alias)) {
                keyStore.deleteEntry(alias);
            }
        } catch (Exception ignored) {
        }
    }

    private void ensureKey(String alias) throws Exception {
        KeyStore keyStore = KeyStore.getInstance(ANDROID_KEYSTORE);
        keyStore.load(null);
        if (keyStore.containsAlias(alias)) {
            return;
        }

        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEYSTORE);
        KeyGenParameterSpec spec = new KeyGenParameterSpec.Builder(
                alias,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setRandomizedEncryptionRequired(true)
                .build();
        generator.init(spec);
        generator.generateKey();
    }

    private SecretKey getSecretKey(String alias) throws Exception {
        KeyStore keyStore = KeyStore.getInstance(ANDROID_KEYSTORE);
        keyStore.load(null);
        return (SecretKey) keyStore.getKey(alias, null);
    }
}

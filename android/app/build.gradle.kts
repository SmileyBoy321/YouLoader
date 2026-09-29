import java.util.Properties

plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

// Optional release signing: create android/keystore.properties (never commit it) with
// storeFile, storePassword, keyAlias, keyPassword. Without it, release builds are unsigned.
val keystorePropsFile = rootProject.file("keystore.properties")
val keystoreProps = Properties().apply {
    if (keystorePropsFile.exists()) keystorePropsFile.inputStream().use { load(it) }
}

// The download engine ships native binaries (Python, ffmpeg, QuickJS) for every CPU type.
// One APK per CPU type keeps each download small; the universal APK works on any phone.
val supportedAbis = listOf("arm64-v8a", "armeabi-v7a", "x86_64", "x86")

android {
    namespace = "com.youloader.app"
    compileSdk = 36

    defaultConfig {
        applicationId = "com.youloader.app"
        minSdk = 24
        targetSdk = 36
        // Android releases share version numbers with the Windows app; 2.2.0 is the first with an APK.
        versionCode = 20200
        versionName = "2.2.0"
        ndk { abiFilters += supportedAbis }
    }

    splits.abi {
        isEnable = true
        reset()
        include(*supportedAbis.toTypedArray())
        isUniversalApk = true
    }

    signingConfigs {
        if (keystorePropsFile.exists()) {
            create("release") {
                storeFile = file(keystoreProps.getProperty("storeFile"))
                storePassword = keystoreProps.getProperty("storePassword")
                keyAlias = keystoreProps.getProperty("keyAlias")
                keyPassword = keystoreProps.getProperty("keyPassword")
            }
        }
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            signingConfig = signingConfigs.findByName("release")
        }
    }

    // The engine runs its binaries straight from the native library folder, so they must stay unpacked.
    packaging {
        jniLibs { useLegacyPackaging = true }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    kotlinOptions {
        jvmTarget = "17"
    }

    buildFeatures {
        viewBinding = true
        buildConfig = true
    }
}

dependencies {
    val youtubedlAndroid = "0.18.1"
    implementation("io.github.junkfood02.youtubedl-android:library:$youtubedlAndroid")
    implementation("io.github.junkfood02.youtubedl-android:ffmpeg:$youtubedlAndroid")

    implementation("androidx.core:core-ktx:1.15.0")
    implementation("androidx.appcompat:appcompat:1.7.0")
    implementation("com.google.android.material:material:1.12.0")
    implementation("androidx.activity:activity-ktx:1.9.3")
    implementation("androidx.lifecycle:lifecycle-runtime-ktx:2.8.7")
    implementation("androidx.recyclerview:recyclerview:1.3.2")

    testImplementation("junit:junit:4.13.2")
}

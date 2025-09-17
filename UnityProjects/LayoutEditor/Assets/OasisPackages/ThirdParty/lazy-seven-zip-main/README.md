# Lazy Seven Zip

Lazy Seven Zip is a wrapper for the popular [SevenZipSharp](https://github.com/squid-box/SevenZipSharp) library.<br>
It provides an easy to use set of methods to easily create and extract archives in Unity within the Editor or at
Runtime.

For the benefit of the user, the source code is easy to follow and can be modified without must difficulty.

Please note!<br>
This library does not include creating or extracting archives via Stream.

## Lazy Archive Class

The Lazy Archive Wrapper Class consists of 4 methods, 2 Synchronous and 2 Asynchronous methods.<br>
This allows you to create standard or encrypted archives using the Files and Folders in your current Unity Project.

The methods in the class are shown below.

### Synchronous Methods

```csharp
public static void Archive(string outArchive, 
                           string[] inFiles, 
                           OutArchiveFormat archiveFormat, 
                           CompressionLevel compressionLevel = CompressionLevel.Normal)
{
...
}
```

<br>

```csharp
public static void Archive(string outArchive,
                           string[] inFiles,
                           string password,
                           OutArchiveFormat archiveFormat,
                           bool encryptHeaders = false,
                           ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
                           CompressionLevel compressionLevel = CompressionLevel.Normal)
{
...
}
```

### Asynchronous Methods

```csharp
public static async Task Archive(string outArchive, 
                           string[] inFiles, 
                           OutArchiveFormat archiveFormat, 
                           CompressionLevel compressionLevel = CompressionLevel.Normal)
{
...
}
```

<br>

```csharp
public static async Task Archive(string outArchive,
                           string[] inFiles,
                           string password,
                           OutArchiveFormat archiveFormat,
                           bool encryptHeaders = false,
                           ZipEncryptionMethod encryptionMethod = ZipEncryptionMethod.Aes128,
                           CompressionLevel compressionLevel = CompressionLevel.Normal)
{
...
}
```

## Lazy Extractor Class

The Lazy Extractor Wrapper Class consists of 2 Extraction methods, 1 Synchronous and 1 Asynchronous method.<br>
This allows you to extract standard or encrypted archives anywhere within the Unity Editor or on your Local Machine.

The methods in the class are shown below.

### Synchronous

```csharp
public static void Extract(string outPath, string inArchive, string password = "")
{
...
}
```

### Asynchronous

```csharp
public static async Task ExtractAsync(string outPath, string inArchive, string password = "")
{
...
}
```

## Examples - Creating an Archive

### Method 1 - Archive no Password

```csharp

public void ArchiveTest(string[] filesToArchive)
{
    LazyArchiver.ArchiveAsync(@"Assets/test.7z", filesToArchive , OutArchiveFormat.SevenZip);
}

public async Task ArchiveTestAsync(string[] filesToArchive)
{
    await LazyArchiver.ArchiveAsync(@"Assets/test.7z", filesToArchive, OutArchiveFormat.SevenZip);
}

```

### Method 2 - Archive with Password

```csharp

public void ArchivePasswordTest(string password, string[] filesToArchive)
{
    LazyArchiver.ArchiveAsync(@"Assets/test.7z", filesToArchive , password, OutArchiveFormat.SevenZip);
}

public async Task ArchivePasswordTestAsync(string password, string[] filesToArchive)
{
    await LazyArchiver.ArchiveAsync(@"Assets/test.7z", filesToArchive, password, OutArchiveFormat.SevenZip);
}

```

## Examples - Extracting an Archive

### Method 1 - Extract an Archive with no Password

```csharp

public void ExtractArchiveTest(string outputPath)
{
    LazyExtractor.Extract(outputPath, @"Assets/test.7z");
}

public async Task ExtractArchiveTestAsync(string outputPath)
{
    await LazyExtractor.ExtractAsync(outputPath, @"Assets/test.7z");
}

```

### Method 2 - Extract an Archive with a Password

```csharp

public void ExtractArchivePasswordTest(string outputPath, string password)
{
    LazyExtractor.Extract(outputPath, @"Assets/test.7z", password);
}

public async Task ExtractArchivePasswordTestAsync(string outputPath, string password)
{
    await LazyExtractor.ExtractAsync(outputPath, @"Assets/test.7z", password);
}

```

# Work In Progress

+ Editor Window to Create Archives similar to the 7zip Archive Creator Window

# Credits

+ [SevenZipSharp](https://github.com/squid-box/SevenZipSharp) created by [ToMap](https://github.com/tomap) and currently
  maintained by [Squid-Box](https://github.com/squid-box)
+ [7Zip Library](https://www.7-zip.org/) by [Igor Pavlov](https://sourceforge.net/u/ipavlov/profile/)
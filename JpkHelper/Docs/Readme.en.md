# JPKHelper
This software is intend to assist in preparing and sending files for the Polish Ministry of Finance JPK system.
Currently it is capable of two operations: preparing files to be sent, and sending files based on an `InitUpload.xml` file.
Validating files requires an internet connection, as the schemas published by the Ministry of Finance have references to files hosted on their server.

To see a complete list of available commands, simply run the program from the console:
```
jpkhelper
```
To learn more about a specific command, use the `help` verb:
```
jpkhelper help make
```

## Preparing files for upload

The `JpkHelper make` command validates and prepares a set of files to be sent.
Validation schema is picked based on the name of the file.
For instance, files for ITP (Informacje o Transakcjach Płatniczych) must start with `ITP` (example: `ITP_15022024.xml`).

To see a complete list of flags and their meanings, use the help command:
```
jpkhelper help make
```

### Basic usage

The list of files is specified using the `--files` or `-f` flags.
For example:
```
jpkhelper -f files/ITP_15.xml files/ITP_34.xml
```
By default the result will be placed in in the `out` directory.
To change it, specify the `-o` or `--output` flag:
```
jpkhelper -f files/ITP_15.xml -o jpk
```
By default, the file encryption keys will be secured using the **test enviroment certificate**.
To use it in production, either specify the enviroment type with `-e`/`--enviroment-type` flag:
```
jpkhelper -f files/ITP_15.xml -o jpk -e Production
```
Or specify the path to a file with the certificate you want to use to encrypt the keys.
Keep in mind that the certificate must be in the PEM format.
This option is not recomended, as the program is preloaded with up-to-date certificates.
It also validates that the certificate is not expired. 
If that happens, visit https://github.com/Agrabski/JpkHelper to download a new version of the software.
```
jpkhelper -f files/ITP_15.xml -o jpk --certificate-file c:\certificates\current-mf-certificate.pem
```

### Full usage example

In this scenario, you have the following directory structure (with file sizes noted):
```
C:\
├── ITP_Files
|   ├── ITP_1.xml (10MB)
|   ├── ITP_2.xml (320MB)
|   ├── ITP_3.xml (50MB)
```
To prepare these files for upload to JPK, in the **production** envoriment, you can use the following command (keep in mind that you do not have to use absolute paths):
```
jpkhelper -f C:\ITP_Files\ITP_1.xml C:\ITP_Files\ITP_2.xml C:\ITP_Files\ITP_3.xml -e Production -o C:\ITP_Ready
```
After the program is executed, the following directory structure will exist:
```
C:\
├── ITP_Files
|   ├── ITP_1.xml
|   ├── ITP_2.xml
|   ├── ITP_3.xml
├── ITP_Ready
|   ├── ITP_1-initUpload.xml
|   ├── ITP_2-initUpload.xml
|   ├── ITP_3-initUpload.xml
|   ├── ITP_1.xml.zip.001.aes
|   ├── ITP_2.xml.zip.001.aes
|   ├── ITP_2.xml.zip.002.aes
|   ├── ITP_2.xml.zip.003.aes
|   ├── ITP_2.xml.zip.004.aes
|   ├── ITP_2.xml.zip.005.aes
|   ├── ITP_3.xml.zip.001.aes
```
The `C:\ITP_Ready` directory now contains all the files needed to upload into JPK servers.
The progam will also output a ready-to-use command, to perfrom this upload.

Note that the `ITP_2.xml` was split into several `.zip.XXX.aes` files.
This is due to Ministry of Finance requirement for compressed files to be split into 60MB chunks before encryption.
For simplicity, in this example we assumed that compression does not reduce file sizes.
A single `.xml` file producing several `.zip.XXX.aes` files is normal.

### Currently supported file types
Currently the `systemCode` field is hard-coded to ITP (2)
- [ ] ITP (1)
- [x] ITP (2)
- [ ] ITP-Z (1)
- [ ] ITP-Z (2)
- [ ] JPK_v7M
- [ ] JPK_v7K
- [ ] CUK
- [ ] ALK
- [ ] JPK_GV

## Uploading files

Uploading the files is a straight-forward process.
The `send` command works on the `initUpload.xml` file.
It takes the list of files to be sent, from the contents of `initUpload.xml` and looks for them in the same directory as the `initUpload.xml`.

### Usage example
Lets assume you have the following directory structure:
```
C:\
├── ITP_Ready
|   ├── InitUpload.xml
|   ├── ITP_1.xml.zip.001.aes
|   ├── ITP_2.xml.zip.001.aes
|   ├── ITP_2.xml.zip.002.aes
|   ├── ITP_2.xml.zip.003.aes
|   ├── ITP_2.xml.zip.004.aes
|   ├── ITP_2.xml.zip.005.aes
|   ├── ITP_3.xml.zip.001.aes

```
Where all `.xml.zip.XXX.aes` files are mentioned in the `InitUpload.xml`.
To perform the upload, use the following commands:
Production enviroment:
```
jpkHelper send -p c:\ITP_Ready\InitUpload.xml -e Production
```
Test enviroment:
```
jpkHelper send -p c:\ITP_Ready\InitUpload.xml -e Test
```
# JPKHelper
Oprogramowanie to ma na celu wspomaganie przygotowania i wysyłania plików do systemu JPK Ministerstwa Finansów. 
Obecnie umożliwia wykonywanie dwóch operacji: przygotowanie plików do wysłania oraz wysyłanie plików na podstawie pliku InitUpload.xml. 
Walidacja plików wymaga połączenia z internetem, ponieważ schematy opublikowane przez Ministerstwo Finansów zawierają odwołania do plików znajdujących się na ich serwerze.

Aby zobaczyć pełną listę dostępnych poleceń, wystarczy uruchomić program z poziomu konsoli:
```
jpkhelper
```
To learn more about a specific command, use the `help` verb:
```
jpkhelper help make
```

## Przygotowanie plików do wysyłki

Polecenie JpkHelper make weryfikuje i przygotowuje zestaw plików do wysłania. 
Schemat walidacji jest wybierany na podstawie nazwy pliku. Na przykład, pliki dla ITP (Informacje o Transakcjach Płatniczych) muszą zaczynać się od ITP (przykład: ITP_15022024.xml).

Aby zobaczyć pełną listę flag i ich znaczenie, użyj polecenia pomocy:
```
jpkhelper help make
```

### Podstawowe użycie

Lista plików jest określona za pomocą flag --files lub -f. 
Na przykład:
```
jpkhelper -f files/ITP_15.xml files/ITP_34.xml
```
Domyślnie wynik zostanie umieszczony w katalogu out.
Aby to zmienić, należy użyć flagi -o lub --output:
```
jpkhelper -f files/ITP_15.xml -o jpk
```
Domyślnie klucze szyfrowania plików będą zabezpieczone za pomocą certyfikatu środowiska testowego. 
Aby używać go w środowisku produkcyjnym, należy określić typ środowiska za pomocą flagi -e/--environment-type:
```
jpkhelper -f files/ITP_15.xml -o jpk -e Production
```
Lub określ ścieżkę do pliku z certyfikatem, który chcesz użyć do szyfrowania kluczy.
Pamiętaj, że certyfikat musi być w formacie PEM.
Opcja ta nie jest zalecana, ponieważ program jest wstępnie załadowany z aktualnymi certyfikatami.
Program także weryfikuje, czy certyfikat nie jest przeterminowany.
Jeśli tak się stanie, odwiedź https://github.com/Agrabski/JpkHelper, aby pobrać nową wersję oprogramowania.
```
jpkhelper -f files/ITP_15.xml -o jpk --certificate-file c:\certificates\current-mf-certificate.pem
```

### Pełny przykład użycia

W tym scenariuszu masz następującą strukturę katalogów (z uwzględnieniem rozmiarów plików):
```
C:\
├── ITP_Files
|   ├── ITP_1.xml (10MB)
|   ├── ITP_2.xml (320MB)
|   ├── ITP_3.xml (50MB)
```
Aby przygotować te pliki do przesłania do JPK, w środowisku produkcyjnym, możesz użyć następującego polecenia (pamiętaj, że nie musisz używać ścieżek bezwzględnych):
```
jpkhelper -f C:\ITP_Files\ITP_1.xml C:\ITP_Files\ITP_2.xml C:\ITP_Files\ITP_3.xml -e Production -o C:\ITP_Ready
```
Po wykonaniu programu, powstanie poniższa struktura katalogów:
```
C:\
├── ITP_Files
|   ├── ITP_1.xml
|   ├── ITP_2.xml
|   ├── ITP_3.xml
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
Katalog C:\ITP_Ready teraz zawiera wszystkie pliki potrzebne do przesłania na serwery JPK. Program wygeneruje również gotowe polecenie do wykonania tego przesyłania.

Zauważ, że plik ITP_2.xml został podzielony na kilka plików .zip.XXX.aes.
Jest to zgodne z wymaganiami Ministerstwa Finansów, które nakazują podział skompresowanych plików na kawałki o rozmiarze 60 MB przed szyfrowaniem. 
Dla uproszczenia, w tym przykładzie założyliśmy, że kompresja nie zmienia rozmiarów plików. 
Jeden plik .xml, który generuje kilka plików .zip.XXX.aes, jest normalnym zjawiskiem.

### Obecnie wspierane typy plików
Obecnie pole `systemCode` jest ustawione na stale na ITP (2)
- [ ] ITP (1)
- [x] ITP (2)
- [ ] ITP-Z (1)
- [ ] ITP-Z (2)
- [ ] JPK_v7M
- [ ] JPK_v7K
- [ ] CUK
- [ ] ALK
- [ ] JPK_GV

## Wysyłanie plików

Przesyłanie plików jest prostym procesem. 
Polecenie send działa z plikiem initUpload.xml. 
Pobiera listę plików do wysłania z zawartości initUpload.xml i szuka ich w tym samym katalogu co initUpload.xml.

### Przykład użycia
Załóżmy, że masz następującą strukturę katalogów:
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
Gdzie wszystkie pliki .xml.zip.XXX.aes są wymienione w pliku InitUpload.xml. 
Aby przeprowadzić przesyłanie, użyj następujących poleceń: 
Środowisko produkcyjne:
```
jpkHelper send -p c:\ITP_Ready\InitUpload.xml -e Production
```
Środowisko testowe:
```
jpkHelper send -p c:\ITP_Ready\InitUpload.xml -e Test
```
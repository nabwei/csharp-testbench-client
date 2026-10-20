# C# Testbench Client

[![CI](https://github.com/nabwei/csharp-testbench-client/actions/workflows/ci.yml/badge.svg)](https://github.com/nabwei/csharp-testbench-client/actions/workflows/ci.yml)

Kleiner .NET-8-Konsolenclient für den unabhängigen öffentlichen
[measurement-testbench](https://github.com/nabwei/measurement-testbench).
Der C#-Code ist neu geschrieben; als Schnittstellenvertrag dient ausschließlich
das [öffentliche Demonstrationsprotokoll](https://github.com/nabwei/measurement-testbench/blob/67fc7b195b1c013fc08f7e677650220d113f05a3/docs/protocol.md).
Keine Verbindung zu einem Institutsprotokoll.

Mit KI-Unterstützung entwickelt, einschließlich Implementierung, Tests und
Dokumentation. Die automatisierten Prüfungen sind im Workflow nachvollziehbar.

## Ablauf

Der Client startet den C++-Simulator als Unterprozess, sendet 16 hexkodierte
Anfragen (Sequenz 0 bis 15) über stdin und schließt den Eingabestrom.
Er liest Antworten und stderr parallel, begrenzt die Laufzeit auf zehn Sekunden
und prüft Länge, Header, CRC-16 und Gerätestatus vor Übernahme einer Messung.
CRC: Polynom `0x1021`, Start `0xffff`, keine Spiegelung, kein abschließendes XOR;
CRC-Feld und Zahlenfelder sind Little Endian.

Doppelte, nicht angeforderte und vertauschte Sequenzen werden gemeldet.
Nicht akzeptierte Sequenzen bleiben in `MissingSequences`. Zeitintervalle
außerhalb von 1000 ± 50 Ticks erzeugen Anomalien. Ausgabe: JSON mit Samples,
fehlenden Sequenzen, Anomalien und `Passed`.

## Bauen und ausführen

Voraussetzungen: .NET SDK 8.0.426 (in `global.json` fixiert), CMake ≥ 3.24, C++17-Compiler; für den
Integrationstest zusätzlich Python 3.12. Im Stamm dieses Repositories:

```sh
git clone https://github.com/nabwei/measurement-testbench.git simulator
git -C simulator checkout 67fc7b195b1c013fc08f7e677650220d113f05a3
cmake -S simulator -B sim-build -DBUILD_TESTING=OFF
cmake --build sim-build --config Release
dotnet test Tests/Tests.csproj --configuration Release
dotnet run --project Client --configuration Release -- sim-build/bin/sensor_sim clean
```

Unter Windows endet der Simulatorpfad auf `sensor_sim.exe`.
`dotnet test` baut auch das referenzierte Konsolenprojekt.
Ein absichtlicher Fehlerfall:

```sh
dotnet run --project Client --configuration Release -- sim-build/bin/sensor_sim corrupt
```

Exitcodes: `0` alle Prüfungen bestanden, `1` gemeldete Anomalie oder fehlende
Antwort, `2` Aufruf-/Simulatorfehler. Bei `corrupt` bleibt Sequenz 5 wegen
CRC-Fehler unberücksichtigt; außerdem fällt die entstehende Zeitlücke auf.

## Tests und CI

xUnit prüft den bekannten CRC-Vektor `123456789 → 29b1`, einen unabhängig
berechneten Request/Reply-Vektor, Parsing, gültige Fehlerantworten, beschädigte
Frames, Sequenzregeln und inklusive Toleranzgrenzen. Keine Simulator-Mocks.

```sh
python test_integration.py sim-build/bin/sensor_sim
```

Der Integrationstest verwendet den echten Simulator für alle sechs Szenarien.
Die CI führt xUnit und Integration auf Windows und Ubuntu aus und fixiert die
Simulatorversion durch ihren Commit. Beide regulären Jobs verwenden SDK 8.0.426
wie `global.json` und starten im Repository-Verzeichnis. Ein zusätzlicher
Ubuntu-Build prüft die Kompilierung mit SDK 8.0.100. Nur dieser Kompatibilitätsjob
überschreibt seine ausgecheckte `global.json` temporär, damit tatsächlich der
ältere Compiler statt eines vorinstallierten neueren SDK verwendet wird.

## Limitations

- Nur synthetische Simulation über Text-Pipes, kein UART-/USB-Treiber.
- Feste Anfragefolge 0..15, kein Sequenz-Wraparound und keine Retransmission.
- Kein Streaming, keine Messung realer Latenz und keine Durchsatzbehauptung.
- Das Timingmodell ist das veröffentlichte Simulatormodell; ein CRC ist keine
  Authentifizierung. Keine Aussage über reale Sensorqualität.
- Die Ausgaben sind vergleichbar, aber nicht identisch zum Python-JSON-Schema;
  konstante Timestamp-Bits werden hier nicht separat analysiert.
- SDK und Pakete müssen zum Bauen von Microsoft/NuGet verfügbar sein.
  Der frühe SDK-Build ist ein Compiler-Kompatibilitätstest, keine Empfehlung
  zum Einsatz einer veralteten Runtime.

## English summary

A from-scratch .NET 8 client for the public standalone C++ simulator. It validates
CRC, reply layout, status, sequence membership/order and synthetic timing, then
reports JSON anomalies. xUnit and real-simulator integration tests run on Windows
and Ubuntu. No institute protocol, hardware transport or performance claim.
Developed with AI assistance for code, tests and documentation. The regular
build pins SDK 8.0.426; an isolated Ubuntu build checks SDK 8.0.100 compatibility.

# LyricsPro

Exibidor de letras de músicas, Bíblia e mídia para projetor.

## Pré-requisitos

1. Instale o **.NET 8 SDK**: https://dotnet.microsoft.com/download/dotnet/8.0
2. (Opcional) Visual Studio 2022 ou Rider para editar com IntelliSense

## Como rodar

```powershell
cd src\LyricsPro
dotnet run
```

## Stack

- **Avalonia UI 11** — UI cross-platform (Windows 10/11 + Linux)
- **CommunityToolkit.Mvvm** — MVVM leve e sem reflexão
- **Tema escuro** — Preto `#111111` + Laranja escuro `#D4610A`

## Funcionalidades planejadas

- [x] Tela inicial com sidebar + busca + thumbnails
- [ ] Buscador de letras na internet
- [ ] Biblioteca de letras salvas (SQLite local)
- [ ] Módulo Bíblia (NTLH / Almeida Corrigida)
- [ ] Biblioteca de imagens de fundo
- [ ] Biblioteca de áudios MP3
- [ ] Biblioteca de vídeos
- [ ] Modo Projetor (segunda tela)

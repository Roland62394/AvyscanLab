# Messages fréquents dans la fenêtre de sortie

Si vous voyez beaucoup de messages comme:
- `Faute d’orthographe - ... n’est pas un mot`
- `N'utilisez pas de méthodes Enumerable sur les collections indexables`
- `Utilisez "GeneratedRegexAttribute" ...`

c'est généralement **normal**: ce sont surtout des **suggestions d'analyseurs IDE** (orthographe, style, performance), pas des erreurs de compilation.

## Ce qui est important

- **Erreur** (`error`) : à corriger, la build échoue.
- **Avertissement** (`warning`) : recommandé, mais la build peut réussir.
- **Suggestion / message d'orthographe** : aide à la qualité, sans bloquer la build.

## Vérification rapide

Lancez la build CLI pour valider l'état réel du projet:

```bash
dotnet build -nologo
```

Si la commande termine avec `Build succeeded`, les messages d'orthographe/style vus dans Visual Studio ne bloquent pas le projet.

## Pourquoi vous en voyez beaucoup

Le projet contient des termes techniques (`degrain`, `gammac`, `removedirt`, `Omin/Omax`) et des textes localisés (allemand/espagnol), souvent considérés comme "fautes" par les outils de correction orthographique.

## Réduction du bruit (optionnel)

Vous pouvez réduire ces messages dans l'IDE:
- désactiver le correcteur orthographique sur certains fichiers (ressources, docs techniques),
- ajouter un dictionnaire personnalisé pour les termes techniques,
- baisser la sévérité des suggestions de style/performance qui ne sont pas prioritaires.

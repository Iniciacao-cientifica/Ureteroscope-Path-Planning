# Meshy Urinary System v002 — Marco 3.1

## Arquivo para abrir no Maya

Abra `Source/Meshy_Urinary_System_v002.ma` no Maya 2027.

A cena usa centímetros e contém dois grupos:

- `MeshyUrinaryVisual_Export`: rim direito, ureteres e bexiga destinados ao Unity;
- `REFERENCE_ONLY`: rim esquerdo Meshy e malha doadora de textura, ocultos e excluídos do FBX final.

O rim esquerdo aprovado do projeto continua sendo o `Kidney_Game_v002` e não
foi substituído.

## Resultado técnico

- fonte de alta resolução: `15.979` triângulos e `46,5 cm` de altura;
- FBX visual final: `11.026` triângulos, pois o rim esquerdo Meshy foi excluído;
- duas malhas exportadas: `Meshy_RightKidney` e `Meshy_UretersAndBladder`;
- UVs transferidos do primeiro pacote Meshy;
- quatro mapas PBR religados para a pasta `Imports`;
- componentes non-manifold corrigidos automaticamente;
- FBX reimportado no Maya com escala, nomes, UVs e topologia preservados.
- lateralidade corrigida no pipeline: o eixo X do FBX é invertido entre Maya e
  Unity, portanto o shell externo fechado correto é exportado como rim direito.

O relatório reproduzível fica em `Documentation/meshy_v002_report.json` e o
FBX destinado ao Unity em `Exports/Meshy_Urinary_Visual_v002.fbx`.

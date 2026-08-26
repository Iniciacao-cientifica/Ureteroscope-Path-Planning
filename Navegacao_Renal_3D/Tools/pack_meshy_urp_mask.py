"""Empacota os mapas Meshy no formato do material URP Lit.

R = metallic, G = ambient occlusion neutro, B = reservado,
A = smoothness (1 - roughness).
"""

from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(
    r"C:\Users\pedro\OneDrive\Área de Trabalho\Navegacao_Renal_3D_Maya_v01"
    r"\Candidates\Meshy_Urinary_System_v002\Imports"
)
METALLIC = ROOT / "T_MeshyUrinary_Metallic_v002.png"
ROUGHNESS = ROOT / "T_MeshyUrinary_Roughness_v002.png"
OUTPUT = ROOT / "T_MeshyUrinary_MaskMap_v002.png"


def main() -> None:
    with Image.open(METALLIC) as metallic_image, Image.open(ROUGHNESS) as roughness_image:
        metallic = metallic_image.convert("L")
        roughness = roughness_image.convert("L")
        if metallic.size != roughness.size:
            raise ValueError(f"Dimensões incompatíveis: {metallic.size} x {roughness.size}")
        white = Image.new("L", metallic.size, 255)
        black = Image.new("L", metallic.size, 0)
        smoothness = ImageChops.invert(roughness)
        packed = Image.merge("RGBA", (metallic, white, black, smoothness))
        packed.save(OUTPUT, format="PNG", optimize=True)
    print(OUTPUT)


if __name__ == "__main__":
    main()

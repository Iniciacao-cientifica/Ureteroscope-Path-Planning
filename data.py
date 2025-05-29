import os
import numpy as np
from PIL import Image

def carregar_imagens_binarias(diretorio):
    pilha = []
    for nome_arquivo in sorted(os.listdir(diretorio)):
        if nome_arquivo.lower().endswith('.png'):
            caminho_imagem = os.path.join(diretorio, nome_arquivo)
            imagem = Image.open(caminho_imagem).convert('L')
            imagem_binaria = np.array(imagem) > 127
            pilha.append(imagem_binaria.astype(np.uint8))
    return np.stack(pilha)

def extrair_coordenadas_brancas(volume_binario):
    z_idx, y_idx, x_idx = np.where(volume_binario == 1)
    coords = np.stack([x_idx, y_idx, z_idx], axis=1)
    return coords
import data
import numpy as np
import time
import path_planning
from curves import savgol_curve
from curves import bspline_curve
from curves import kalman_curve
from curves import laplacian_curve
import metrics
from view import Viewer3D

directory = 'map/'  # Substitua pelo seu caminho
volume = data.carregar_imagens_binarias(directory)
print("Volume shape:", volume.shape)

points = data.extrair_coordenadas_brancas(volume)
print("Total de pontos navegáveis:", points.shape[0])
print(np.unique(volume, return_counts=True))

kidney_stone = (220, 174, 227 - 217)  # (x, y, z)
#start_point = tuple(points[np.argmin(points[:, 2])])
start_point = (253, 355, 20)
start_idx = start_point[::-1]  # (z, y, x)
end_idx = kidney_stone[::-1]

start_time = time.time()
path = path_planning.path_plan(volume, start_idx, end_idx)

if path:
    print("Caminho encontrado com", len(path), "pontos.")
    path = [p[::-1] for p in path]  # (z, y, x) → (x, y, z)
    #pontos_filtrados = reduzir_pontos_min_distancia(caminho_xyz, min_dist=1.0)
    #print("Pontos reduzidos:", len(pontos_filtrados), "pontos.")
    #curve = savgol_curve(path, order=9)
    curve = bspline_curve(path, degree=5, smooth_factor=30, n=len(path))
    #curve = kalman_curve(path, process_noise=0.05, measurement_noise=0.2)
    #curve = laplacian_curve(path, iterations=8, lambda_factor=0.3)

    # Validação crítica
    metricas = metrics.calcular_metricas_completas(path, curve, volume, kidney_stone, start_time)
    relatorio = metrics.verificar_extrapolacao(curve, volume, limiar_distancia=0.1)
    
    
    metrics.print_relatorio_completo(metricas, relatorio)
    # Exportação de dados
    #exportar_resultados(curve, metricas)
    
else:
    print("Caminho não encontrado.")

viewer = Viewer3D(volume, path, kidney_stone, start_point, curve)
viewer.show()

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
from optimization import optimize_all
from optimization import load_best_results

directory = 'map/'  # Substitua pelo seu caminho
volume = data.carregar_imagens_binarias(directory)
print("Volume shape:", volume.shape)

points = data.extrair_coordenadas_brancas(volume)
print("Total de pontos navegáveis:", points.shape[0])

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
    path = path_planning.reduzir_pontos_porcentagem(path, porcentagem=1.0)
    print("Pontos reduzidos:", len(path), "pontos.")

    
    best_params = load_best_results("resultados_completos.csv")

    savgol_params = best_params['Savitzky-Golay']['params']
    bspline_params = best_params['B-Spline']['params']
    laplacian_params = best_params['Laplaciana']['params']

    savgol = savgol_curve(path, window_ratio=savgol_params['window_ratio'], order=savgol_params['order'])
    bspline = bspline_curve(path, order=bspline_params['order'], smooth_factor=bspline_params['smooth_factor'])
    laplacian = laplacian_curve(path, iterations=laplacian_params['iterations'], lambda_factor=laplacian_params['lambda_factor'])
    
    curve = savgol

else:
    print("Caminho não encontrado.")

viewer = Viewer3D(volume, path, kidney_stone, start_point, curve)
viewer.show()

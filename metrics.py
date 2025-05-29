import numpy as np
from scipy.interpolate import splprep, splev
from scipy.ndimage import map_coordinates
from scipy.spatial.distance import directed_hausdorff
from scipy.spatial import KDTree
import pyvista as pv
import time


def calcular_curvatura_torsao(curva):
    curva_np = np.array(curva)
    tck, u = splprep(curva_np.T, s=0)
    
    # Primeira e segunda derivadas
    der1 = splev(u, tck, der=1)
    der2 = splev(u, tck, der=2)
    
    curvatura = []
    torsao = []
    for i in range(len(u)):
        dr = np.array([d[i] for d in der1])
        ddr = np.array([d[i] for d in der2])
        
        # Curvatura
        cross = np.cross(dr, ddr)
        curv = np.linalg.norm(cross) / (np.linalg.norm(dr)**3)
        curvatura.append(curv)
        
        # Torção (requer terceira derivada)
        der3 = splev(u[i], tck, der=3)
        dddr = np.array(der3)
        tors = np.dot(cross, dddr) / (np.linalg.norm(cross)**2) if np.linalg.norm(cross) != 0 else 0
        torsao.append(tors)
    
    return np.array(curvatura), np.array(torsao)

def calcular_comprimento(curva):
    return np.sum(np.linalg.norm(np.diff(curva, axis=0), axis=0))


def hausdorff_dist(original, curva):
    return max(directed_hausdorff(original, curva)[0], 
           directed_hausdorff(curva, original)[0])

def calcular_mse(original, curva):
    # Interpolar pontos correspondentes
    #return np.mean([euclidean(p, c) ** 2 for p, c in zip(original, curva[:len(original)])])
    from scipy.interpolate import interp1d
    t_orig = np.linspace(0, 1, len(original))
    t_curva = np.linspace(0, 1, len(curva))
    interp_curva = interp1d(t_curva, curva, axis=0)
    curva_interp = interp_curva(t_orig)
    return np.mean(np.linalg.norm(original - curva_interp, axis=1)**2)

def acuracia_final(curva, alvo):
    return np.linalg.norm(curva[-1] - alvo)

def distancia_minima_paredes(curve, volume):
    """Calcula a distância mínima do caminho às paredes em todo o percurso"""
    obstacle_points = np.argwhere(volume == 0)
    tree = KDTree(obstacle_points)
    distancias = []
    
    for ponto in curve:
        # Converte para coordenadas do volume (z,y,x)
        z, y, x = ponto[2], ponto[1], ponto[0]
        dist, _ = tree.query([[z, y, x]])
        distancias.append(dist)
    
    return np.min(distancias)

def pontos_proximos_obstaculos(curve, volume, threshold):
    """Conta pontos próximos demais de obstáculos"""
    obstacle_points = np.argwhere(volume == 0)
    tree = KDTree(obstacle_points)
    count = 0
    
    for ponto in curve:
        z, y, x = ponto[2], ponto[1], ponto[0]
        dist, _ = tree.query([[z, y, x]])
        if dist < threshold:
            count += 1
    
    return count

def exportar_resultados(curve, metricas):
    """Exporta dados para análise posterior"""
    # Salva curva
    np.savetxt("curva_suavizada.csv", curve, 
              delimiter=";", 
              header="x;y;z", 
              comments='',
              fmt='%.4f')

def verificar_extrapolacao(curva, volume, limiar_distancia=1.0):
    """
    Verifica pontos fora do volume com:
    - Correção na ordem das coordenadas
    - Verificação precisa dos limites
    - Interpolação correta
    """
    
    # 1. Pré-processamento do volume
    pontos_volume = np.argwhere(volume == 1)  # Pontos válidos em (z,y,x)
    tree_volume = KDTree(pontos_volume)
    curva_np = np.array(curva)

    # 2. Verificação refinada
    pontos_fora = []
    distancias = []
    for idx, ponto in enumerate(curva_np):
        # Converte para coordenadas do volume (x,y,z) → (z,y,x)
        z = ponto[2]
        y = ponto[1]
        x = ponto[0]
        
        # Verificação de limites CORRIGIDA
        if (z < 0 or z >= volume.shape[0] or 
            y < 0 or y >= volume.shape[1] or 
            x < 0 or x >= volume.shape[2]):
            pontos_fora.append(idx)
            distancias.append(np.inf)
            continue

        # Interpolação CORRIGIDA (ordem z,y,x)
        try:
            valor = map_coordinates(
                volume, 
                [[z], [y], [x]],  # Coordenadas na ordem (z,y,x)
                order=1,
                mode='nearest'
            )[0]
        except:
            valor = 0

        # Cálculo da distância se estiver em obstáculo
        if valor < 0.5:
            dist, _ = tree_volume.query([z, y, x])
            if dist > limiar_distancia:
                pontos_fora.append(idx)
                distancias.append(dist)

    # 3. Relatório (mesmo código)
    relatorio = {
        'total_pontos': len(curva_np),
        'pontos_fora': len(pontos_fora),
        'percentual_fora': 100 * len(pontos_fora) / len(curva_np),
        'distancia_maxima': np.max(distancias) if distancias else 0,
        'distancia_media': np.mean(distancias) if distancias else 0,
        'limiar': limiar_distancia,
        'indices_fora': pontos_fora,
        'coordenadas_fora': curva_np[pontos_fora] if pontos_fora else []
    }


    return relatorio




def calcular_metricas_completas(path, curve, volume, target_point, start_time):
    """Calcula um conjunto abrangente de métricas para avaliação do caminho"""
    metrics = {}
    
    # Métricas básicas de forma
    metrics['comprimento_original'] = calcular_comprimento(path)
    metrics['comprimento_suavizado'] = calcular_comprimento(curve)
    metrics['reducao_pontos'] = len(path) - len(curve)
    
    # Métricas de suavidade
    curvatura, torsao = calcular_curvatura_torsao(curve)
    metrics['curvatura_media'] = np.nanmean(curvatura)
    metrics['curvatura_max'] = np.nanmax(curvatura)
    metrics['torsao_media'] = np.nanmean(torsao)
    metrics['torsao_max'] = np.nanmax(torsao)
    
    # Métricas de similaridade
    metrics['hausdorff'] = hausdorff_dist(np.array(path), np.array(curve))
    metrics['mse'] = calcular_mse(np.array(path), np.array(curve))
    
    # Métricas de segurança
    metrics['dist_min_paredes'] = distancia_minima_paredes(curve, volume)
    metrics['pontos_risco'] = pontos_proximos_obstaculos(curve, volume, 0.5)
    
    metrics['acuracia_final'] = acuracia_final(np.array(curve), target_point)
    
    # Métricas de desempenho
    metrics['tempo_processamento'] = time.time() - start_time
    
    return metrics

def print_relatorio_completo(metricas, relatorio):
    """Exibe o relatório formatado com todas as métricas"""
    print("\n=== Relatório Completo ===")
    print(f"\n[DESEMPENHO]")
    print(f"Tempo total: {metricas['tempo_processamento']:.2f}s")
    print(f"Comprimento total: {metricas['comprimento_suavizado']:.2f} voxels")
    
    print(f"\n[EFICIÊNCIA]")
    print(f"Redução de pontos: {metricas['reducao_pontos']} (-{metricas['reducao_pontos']/metricas['comprimento_original']:.1%})")
    
    print(f"\n[SEGURANÇA]")
    print(f"Distância mínima às paredes: {metricas['dist_min_paredes']:.2f} voxels")
    print(f"Pontos em zona de risco: {metricas['pontos_risco']}")
    
    print(f"\n[EXTRAPOLAÇÃO]")
    print(f"Pontos totais: {relatorio['total_pontos']}")
    print(f"Pontos fora: {relatorio['pontos_fora']} ({relatorio['percentual_fora']:.2f}%)")
    print(f"Distância máxima à superfície: {relatorio['distancia_maxima']:.2f} voxels")
    print(f"Distância média: {relatorio['distancia_media']:.2f} voxels")

    print(f"\n[QUALIDADE]")
    print(f"Curvatura média: {metricas['curvatura_media']:.4f} (max: {metricas['curvatura_max']:.4f})")
    print(f"Torsão média: {metricas['torsao_media']:.4f} (max: {metricas['torsao_max']:.4f})")
    
    print(f"\n[PRECISÃO]")
    print(f"Erro no ponto final: {metricas['acuracia_final']:.2f} voxels.")
    print(f"Hausdorff: {metricas['hausdorff']:.2f} | MSE: {metricas['mse']:.4f}")
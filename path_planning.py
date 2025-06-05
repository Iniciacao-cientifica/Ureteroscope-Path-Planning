import numpy as np
from queue import PriorityQueue
from scipy.ndimage import label
from scipy.ndimage import gaussian_filter
from scipy.spatial.distance import euclidean

def reconstruct_path(came_from, current, start):
    path = [current]
    while current in came_from:
        current = came_from[current]
        path.append(current)
    path.reverse()
    return path

def h(a, b):
    return np.linalg.norm(np.array(a) - np.array(b))

def is_straight_line_valid(volume, start, end):
    """
    Verifica se todos os voxels ao longo da linha reta entre 'start' e 'end' estão dentro do volume (valor 1).
    Usa o algoritmo de Bresenham 3D para eficiência.
    """
    from itertools import zip_longest

    # Algoritmo de Bresenham 3D
    def interpolate(start, end):
        x0, y0, z0 = start
        x1, y1, z1 = end
        dx = abs(x1 - x0)
        dy = abs(y1 - y0)
        dz = abs(z1 - z0)
        xs = 1 if x1 > x0 else -1
        ys = 1 if y1 > y0 else -1
        zs = 1 if z1 > z0 else -1

        # Direção predominante
        if dx >= dy and dx >= dz:
            err_1 = 2*dy - dx
            err_2 = 2*dz - dx
            for _ in range(dx):
                yield (x0, y0, z0)
                if err_1 > 0:
                    y0 += ys
                    err_1 -= 2*dx
                if err_2 > 0:
                    z0 += zs
                    err_2 -= 2*dx
                err_1 += 2*dy
                err_2 += 2*dz
                x0 += xs
        elif dy >= dx and dy >= dz:
            err_1 = 2*dx - dy
            err_2 = 2*dz - dy
            for _ in range(dy):
                yield (x0, y0, z0)
                if err_1 > 0:
                    x0 += xs
                    err_1 -= 2*dy
                if err_2 > 0:
                    z0 += zs
                    err_2 -= 2*dy
                err_1 += 2*dx
                err_2 += 2*dz
                y0 += ys
        else:
            err_1 = 2*dx - dz
            err_2 = 2*dy - dz
            for _ in range(dz):
                yield (x0, y0, z0)
                if err_1 > 0:
                    x0 += xs
                    err_1 -= 2*dz
                if err_2 > 0:
                    y0 += ys
                    err_2 -= 2*dz
                err_1 += 2*dx
                err_2 += 2*dy
                z0 += zs
        yield (x0, y0, z0)

    # Verifica cada voxel ao longo da linha
    for point in interpolate(start, end):
        z, y, x = point
        if not (0 <= z < volume.shape[0] and 0 <= y < volume.shape[1] and 0 <= x < volume.shape[2]):
            return False
        if volume[z, y, x] == 0:
            return False
    return True

def calculate_repulsion_field(volume, sigma=3.0):
    """
    Calcula o campo de repulsão usando convolução Gaussiana.
    Valores mais altos indicam proximidade de paredes.
    """
    # Inverte o volume: 1 = parede, 0 = área navegável
    obstacle_grid = 1.0 - volume.astype(np.float32)
    
    # Aplica blur Gaussiano para simular influência de múltiplas paredes
    repulsion = gaussian_filter(obstacle_grid, sigma=sigma)
    
    # Normaliza para o intervalo [0, 1]
    repulsion = (repulsion - repulsion.min()) / (repulsion.max() - repulsion.min() + 1e-6)
    return repulsion

def AStar(volume, start, end, 
          repulsion_weight=1.0, 
          allow_diagonal=True,
          repulsion_sigma=3.0, 
          step_validation=True):
    """
    Algoritmo A* com:
    - Campo de repulsão cumulativo
    - Controle de movimentos diagonais
    - Validação de trajetória em linha reta
    """
    shape = volume.shape
    repulsion_field = calculate_repulsion_field(volume, sigma=repulsion_sigma)
    
    open_set = PriorityQueue()
    open_set.put((0, 0, start))
    came_from = {}
    g_score = {start: 0}
    f_score = {start: h(start, end)}
    open_set_hash = {start}
    count = 0

    # Geração de vizinhos
    if allow_diagonal:
        vizinhos = [(dz, dy, dx) for dz in [-1,0,1] for dy in [-1,0,1] for dx in [-1,0,1] if not (dz==dy==dx==0)]
    else:
        vizinhos = [(dz, dy, dx) for dz in [-1,0,1] for dy in [-1,0,1] for dx in [-1,0,1] if (abs(dz)+abs(dy)+abs(dx)) == 1]

    while not open_set.empty():
        current = open_set.get()[2]
        open_set_hash.remove(current)

        if current == end:
            return reconstruct_path(came_from, current, start)

        for dz, dy, dx in vizinhos:
            neighbor = (current[0]+dz, current[1]+dy, current[2]+dx)

            # Verificação básica
            if not (0 <= neighbor[0] < shape[0] and 0 <= neighbor[1] < shape[1] and 0 <= neighbor[2] < shape[2]):
                continue
            if volume[neighbor] == 0:
                continue

            # Validação de trajetória opcional
            if step_validation and not is_straight_line_valid(volume, current, neighbor):
                continue

            # Cálculo do custo adaptativo
            move_cost_base = np.linalg.norm([dz, dy, dx]) if allow_diagonal else 1.0
            repulsion = repulsion_weight * repulsion_field[neighbor]
            total_cost = move_cost_base * (1.0 + repulsion)

            temp_g_score = g_score[current] + total_cost

            if temp_g_score < g_score.get(neighbor, float('inf')):
                came_from[neighbor] = current
                g_score[neighbor] = temp_g_score
                f_score[neighbor] = temp_g_score + h(neighbor, end)
                if neighbor not in open_set_hash:
                    count += 1
                    open_set.put((f_score[neighbor], count, neighbor))
                    open_set_hash.add(neighbor)

    return None

def verificar_conectividade(volume, start, end):
    estrutura = np.ones((3, 3, 3), dtype=np.uint8)  # conectividade 26
    labeled, num = label(volume, structure=estrutura)
    return labeled[start] == labeled[end] and labeled[start] != 0

def reduzir_pontos_min_distancia(pontos, min_dist=1.0):
    if not pontos:
        return []

    reduzidos = [pontos[0]]
    for p in pontos[1:]:
        if euclidean(p, reduzidos[-1]) >= min_dist:
            reduzidos.append(p)
    return reduzidos

def reduzir_pontos_porcentagem(pontos, porcentagem=1.0):
    if not pontos or porcentagem <= 0:
        return []
    
    n = len(pontos)
    n_desejado = max(1, round(n * porcentagem))
    
    if n_desejado >= n:
        return pontos.copy()
    
    if n_desejado == 1:
        return [pontos[0]]
    
    reduzidos = []
    # Calcula os índices uniformemente distribuídos
    indices = [round(i * (n - 1) / (n_desejado - 1)) for i in range(n_desejado)]
    # Remove duplicatas mantendo a ordem
    seen = set()
    for idx in indices:
        if idx not in seen:
            seen.add(idx)
            reduzidos.append(pontos[int(idx)])
    
    return reduzidos

def path_plan(volume, start_idx, end_idx):
    path = None

    if volume[start_idx] == 0 or volume[end_idx] == 0:
        print("Erro: ponto inicial ou pedra fora da via segmentada.")

    elif not verificar_conectividade(volume, start_idx, end_idx):
        print("Pontos desconectados – não há caminho possível entre início e cálculo.")

    else:
        path = AStar(
        volume,                  # Matriz 3D (0 = parede, 1 = livre)
        start_idx,                   # Ponto inicial (z,y,x)
        end_idx,                     # Ponto final (z,y,x)
        repulsion_weight=2.0,    # Força global da repulsão
        allow_diagonal=True,     # Permite movimentos diagonais
        repulsion_sigma=1.0,     # Raio de influência das paredes (maior = mais suave)
        step_validation=True     # Valida cada movimento com raycasting
    )
        
    return path
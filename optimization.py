from curves import savgol_curve
from curves import bspline_curve
from curves import kalman_curve
from curves import laplacian_curve
from tqdm import tqdm
import numpy as np
import metrics
import time
import csv
from datetime import datetime
from itertools import product

from GA import GeneticAlgorithm
from GWO import GreyWolfOptimizer


def create_objective_function(technique_name, path_points, volume, end_point):
    """
    Cria função objetivo que retorna um dicionário de métricas
    para cálculo de fitness ponderado
    """
   
    
    def objective_function(params):
        # Aplicar técnica de suavização
        if technique_name == "Savitzky-Golay":
            window_ratio = params['window_ratio']
            order = int(params['order'])
            smoothed = savgol_curve(path_points, window_ratio, 3, order)
        elif technique_name == "B-Spline":
            smooth_factor = params['smooth_factor']
            order = int(params['order'])
            smoothed = bspline_curve(path_points, order, smooth_factor, n=len(path_points))
        elif technique_name == "Kalman":
            process_noise = params['process_noise']
            measurement_noise = params['measurement_noise']
            smoothed = kalman_curve(path_points, process_noise, measurement_noise)
        elif technique_name == "Laplaciana":
            iterations = int(params['iterations'])
            lambda_factor = params['lambda_factor']
            smoothed = laplacian_curve(path_points, iterations, lambda_factor)
        
        metricas = metrics.calcular_metricas_completas(path_points, smoothed, volume, end_point, time.time())
        relatorio = metrics.verificar_extrapolacao(smoothed, volume, limiar_distancia=0.1)
        # Calcular métricas
        metricsDict = {
            'extrapolations': relatorio['pontos_fora'],
            'mse': metricas['mse'],
            'curvature_mean': metricas['curvatura_media'],
            'curvature_max': metricas['curvatura_max'],
            'torsao_mean': abs(metricas['torsao_media']),
            'torsao_max': abs(metricas['torsao_max']),
            'risk_points': metricas['pontos_risco'],
            'acuracia_final': metricas['acuracia_final']
        }
        
        
        
        return metricsDict
    
    return objective_function

def optimize(technique_name, path_points, volume, end_point,
            custom_weights=None, population_size=50, generations=100):
    """
    Otimiza uma única técnica com pesos personalizados
    :param custom_weights: Pesos personalizados para substituir os padrões
    :return: Melhores parâmetros, fitness e métricas
    """
    # Definir espaço de parâmetros
    param_ranges = {
        "Savitzky-Golay": {'window_ratio': (0.1, 0.5), 'order': (2, 9)},
        "B-Spline": {'smooth_factor': (1.0, 50.0), 'order': (1, 5)},
        "Laplaciana": {'iterations': (5, 10), 'lambda_factor': (0.05, 1.0)}
    }[technique_name]
    
    # Criar função objetivo
    objective_func = create_objective_function(technique_name, path_points, volume, end_point)
    # Criar otimizador com pesos

    def fitness_function(individual):
        metrics = objective_func(individual)
        # Calcular fitness ponderado
        weights = custom_weights or {
            'extrapolations': 2,
            'mse': 0.5,
            'curvature_mean': 2,
            'curvature_max': 1,
            'risk_points': 0.5,
            'acuracia_final': 0.5
        }
        return sum(weights[metric] * metrics[metric] for metric in metrics)
    
    """
    ga = GeneticAlgorithm(
        objective_function=fitness_function,
        param_ranges=param_ranges,
        population_size=population_size,
        generations=generations,
        elitism_count=2,
        tournament_size=5
    )
    """

    # Executar otimização
    #best_params, best_fitness = ga.run()
    #best_metrics = objective_func(best_params)

    gwo = GreyWolfOptimizer(num_wolves=population_size, max_iter=generations)
    best_params, best_fitness, best_metrics = gwo.run(objective_function=objective_func, param_bounds=param_ranges)
    
    print(f"\n=== Resultados para {technique_name} ===")
    print(f"Melhor fitness: {best_fitness:.4f}")
    print("Melhores parâmetros:")
    for param, value in best_params.items():
        print(f"  {param}: {value}")
    
    print("\nMétricas detalhadas:")
    for metric, value in best_metrics.items():
        print(f"  {metric}: {value:.4f}")
    
    return best_params, best_fitness, best_metrics

def grid_search(technique_name, path_points, volume, end_point,
               custom_weights=None, grid_steps=None, csv_writer=None):
    """
    Otimiza uma única técnica usando Grid Search
    :param custom_weights: Pesos personalizados para o cálculo do fitness
    :param grid_steps: Dicionário com número de passos para cada parâmetro
    :return: Melhores parâmetros, fitness e métricas
    """
    # Definir espaço de parâmetros e passos padrão
    param_ranges = {
        "Savitzky-Golay": {
            'window_ratio': (0.1, 0.5),
            'order': (2, 9)
        },
        "B-Spline": {
            'smooth_factor': (1.0, 50.0),
            'order': (1, 5)
        },
        "Laplaciana": {
            'iterations': (5, 10),
            'lambda_factor': (0.1, 1.0)
        }
    }[technique_name]

    # Configurar passos do grid (default = 5 passos por parâmetro)
    if grid_steps is None:
        grid_steps = {param: 5 for param in param_ranges}
    
    # Criar função objetivo
    objective_func = create_objective_function(technique_name, path_points, volume, end_point)
    
    # Pesos padrão
    weights = custom_weights or {
        'extrapolations': 5,
        'mse': 0.1,
        'curvature_mean': 2,
        'curvature_max': 1,
        'torsao_mean': 2,
        'torsao_max': 0.5,
        'risk_points': 0.5,
        'acuracia_final': 0.5
    }
    
    # Função para calcular fitness
    def calculate_fitness(metrics):
        return sum(weights[metric] * metrics[metric] for metric in metrics)
    
    # Gerar grade de parâmetros
    param_grid = {}
    
    for param, (min_val, max_val) in param_ranges.items():
        steps = grid_steps[param]
        
        if isinstance(steps, list):  # Valores explícitos
            param_grid[param] = steps
        elif param in ['order', 'iterations']:  # Parâmetros inteiros
            param_grid[param] = list(range(int(min_val), int(max_val) + 1))
        else:  # Parâmetros float
            param_grid[param] = np.linspace(min_val, max_val, steps).tolist()
    
    # Lista de todas as combinações
    all_params = [dict(zip(param_grid.keys(), values)) 
                 for values in product(*param_grid.values())]
    
    print(f"Testando {len(all_params)} combinações de parâmetros para {technique_name}...")
    
    # Avaliar todas as combinações
    best_fitness = float('inf')
    best_params = None
    best_metrics = None

    writer, fieldnames = csv_writer if csv_writer else (None, None)

    # Configurar barra de progresso
    progress_bar = tqdm(total=len(all_params), 
                        desc=f"{technique_name} Grid Search",
                        unit="comb")
    
    for params in all_params:
        try:
            # Converter parâmetros para tipos apropriados
            converted_params = {}
            for k, v in params.items():
                if k in ['order', 'iterations']:
                    converted_params[k] = int(v)
                else:
                    converted_params[k] = v
            
            # Avaliar combinação
            start_time = time.time()
            metrics = objective_func(converted_params)
            total_time = time.time() - start_time
            fitness = calculate_fitness(metrics)

            if writer:
                record = {
                    'technique': technique_name,
                    'timestamp': datetime.now().isoformat(),
                    'total_time': total_time,  # Preenchido posteriormente
                    'fitness': fitness,
                }
                
                # Adicionar parâmetros específicos
                for param in ['window_ratio', 'order', 'smooth_factor', 'iterations', 'lambda_factor']:
                    record[param] = converted_params.get(param, None)
                
                # Adicionar métricas
                for metric in metrics:
                    record[metric] = metrics[metric]
                
                writer.writerow(record)

            # Atualizar melhor resultado
            if fitness < best_fitness:
                best_fitness = fitness
                best_params = converted_params
                best_metrics = metrics
                
                # Atualizar descrição da barra com melhor fitness atual
                progress_bar.set_postfix(best_fitness=f"{best_fitness:.4f}")
            
            
        except Exception as e:
            # Registrar erro sem interromper
            tqdm.write(f"Erro com parâmetros {params}: {str(e)}")
        finally:
            # Atualizar barra mesmo em caso de erro
            progress_bar.update(1)
    
    progress_bar.close()
    
    if best_params is None:
        raise RuntimeError("Nenhuma combinação válida encontrada!")
    
    print(f"\n=== Resultados para {technique_name} ===")
    print(f"Melhor fitness: {best_fitness:.4f}")
    print("Melhores parâmetros:")
    for param, value in best_params.items():
        print(f"  {param}: {value}")
    
    print("\nMétricas detalhadas:")
    for metric, value in best_metrics.items():
        print(f"  {metric}: {value:.4f}")
    
    return best_params, best_fitness, best_metrics

def optimize_all(path, volume, kidney_stone, output_file="grid_search_report.csv"):
    """
    Executa todos os grid searches e gera relatório CSV
    """
    # Configurações para cada técnica
    techniques = {
        'Savitzky-Golay': {
            'grid_steps': {
                'window_ratio': [0.05, 0.1, 0.15, 0.3, 0.5],
                'order': [2, 3, 5, 7, 9]
            }
        },
        'B-Spline': {
            'grid_steps': {
                'smooth_factor': [1, 5, 15, 30, 50],
                'order': [1, 2, 3, 4, 5]
            }
        },
        'Laplaciana': {
            'grid_steps': {
                'iterations': [3, 5, 7, 9, 10],
                'lambda_factor': [0.1, 0.2, 0.3, 0.5, 1.0]
            }
        }
    }

    # Cabeçalho do CSV
    fieldnames = [
        'technique', 'timestamp', 'total_time',
        'window_ratio', 'order', 'smooth_factor', 
        'iterations', 'lambda_factor',
        'fitness', 'extrapolations', 'mse', 
        'curvature_mean', 'curvature_max',
        'torsao_mean', 'torsao_max', 
        'risk_points', 'acuracia_final'
    ]
    
    best_results = {}

    # Abrir arquivo CSV para escrita
    with open(output_file, 'w', newline='') as csvfile:
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames, delimiter=';')
        writer.writeheader()
        
        # Executar grid search para cada técnica
        for tech_name, config in techniques.items():
            print(f"\n=== Iniciando Grid Search para {tech_name} ===")
            start_time = time.time()
            
            # Executar grid search personalizado
            best_params, best_fitness, best_metrics = grid_search(
                technique_name=tech_name,
                path_points=path,
                volume=volume,
                end_point=kidney_stone,
                grid_steps=config['grid_steps'],
                csv_writer=(writer, fieldnames)  # Passar writer para salvar resultados
            )
            
            total_time = time.time() - start_time
            print(f"Tempo total para {tech_name}: {total_time:.2f} segundos")

            best_results[tech_name] = {
                'params': best_params,
                'fitness': best_fitness,
                'metrics': best_metrics,
                'time': total_time
            }

    print(f"\nRelatório completo salvo em: {output_file}")


    # Imprimir resumo dos melhores resultados
    print("\n=== RESUMO DOS MELHORES PARÂMETROS ===")
    for tech, data in best_results.items():
        print(f"\nTécnica: {tech}")
        print(f"Fitness: {data['fitness']:.4f}")
        print(f"Tempo execução: {data['time']:.2f}s")
        print("Parâmetros:")
        for param, value in data['params'].items():
            print(f"  {param}: {value}")
        print("Métricas:")
        for metric, value in data['metrics'].items():
            print(f"  {metric}: {value:.4f}")
    
    return best_results
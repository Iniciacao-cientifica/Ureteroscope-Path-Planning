import numpy as np
import concurrent.futures

class GreyWolfOptimizer:
    def __init__(self, num_wolves=15, max_iter=50, weights=None):
        self.num_wolves = num_wolves
        self.max_iter = max_iter
        
        # Pesos padrão se não fornecidos
        self.weights = weights or {
            'extrapolations': 0.25,
            'mse': 0.1,
            'curvature_mean': 0.6,
            'curvature_max': 0.6,
            'risk_points': 0.1,
            'acuracia_final': 0.05
        }
        
        # Líderes (agora armazenados como índices)
        self.alpha_idx = None
        self.beta_idx = None
        self.delta_idx = None
        self.alpha_score = float('inf')
        self.beta_score = float('inf')
        self.delta_score = float('inf')
        
    def initialize_population(self, param_names, param_bounds):
        """
        Inicializa a população como array NumPy
        Formato: [num_wolves, num_params]
        """
        population = np.zeros((self.num_wolves, len(param_names)))
        
        for i, (param, (low, high)) in enumerate(param_bounds.items()):
            if isinstance(low, int) and isinstance(high, int):
                population[:, i] = np.random.randint(low, high + 1, size=self.num_wolves)
            else:
                population[:, i] = np.random.uniform(low, high, size=self.num_wolves)
                
        return population, param_names
    
    def calculate_fitness(self, metrics):
        """Calcula o fitness ponderado a partir das métricas"""
        return sum(self.weights[metric] * value for metric, value in metrics.items())
    
    def evaluate_population(self, objective_function, population, param_names):
        """Avalia população em paralelo usando threads"""
        with concurrent.futures.ThreadPoolExecutor() as executor:
            # Converter para dicionários para a função objetivo
            wolves_dicts = [
                {param: population[i, j] for j, param in enumerate(param_names)}
                for i in range(self.num_wolves)
            ]
            
            futures = [executor.submit(objective_function, wolf) for wolf in wolves_dicts]
            
            fitness_scores = []
            metrics_list = []
            
            for future in futures:
                try:
                    metrics = future.result()
                    fitness = self.calculate_fitness(metrics)
                    fitness_scores.append(fitness)
                    metrics_list.append(metrics)
                except Exception as e:
                    print(f"Erro na avaliação: {str(e)}")
                    fitness_scores.append(float('inf'))
                    metrics_list.append(None)
        
        return np.array(fitness_scores), metrics_list
    
    def run(self, objective_function, param_bounds):
        # Extrair nomes e limites dos parâmetros
        param_names = list(param_bounds.keys())
        
        # Inicializar população como array NumPy
        population, param_names = self.initialize_population(param_names, param_bounds)
        
        # Avaliar população inicial
        fitness_scores, metrics_list = self.evaluate_population(objective_function, population, param_names)
        
        # Encontrar índices dos líderes
        sorted_indices = np.argsort(fitness_scores)
        self.alpha_idx = sorted_indices[0]
        self.beta_idx = sorted_indices[1]
        self.delta_idx = sorted_indices[2]
        self.alpha_score = fitness_scores[self.alpha_idx]
        self.beta_score = fitness_scores[self.beta_idx]
        self.delta_score = fitness_scores[self.delta_idx]
        
        # Loop de otimização
        for iter in range(self.max_iter):
            a = 2 - iter * (2 / self.max_iter)  # Coeficiente de decaimento
            
            # Atualizar posições - operação vetorizada
            for i in range(self.num_wolves):
                if i in [self.alpha_idx, self.beta_idx, self.delta_idx]:
                    continue  # Não atualizar os líderes
                
                # Obter valores dos líderes
                alpha_pos = population[self.alpha_idx]
                beta_pos = population[self.beta_idx]
                delta_pos = population[self.delta_idx]
                
                # Calcular novas posições
                r1 = np.random.random(size=len(param_bounds))
                r2 = np.random.random(size=len(param_bounds))
                
                A1 = 2 * a * r1 - a
                C1 = 2 * r2
                D_alpha = np.abs(C1 * alpha_pos - population[i])
                X1 = alpha_pos - A1 * D_alpha
                
                r1 = np.random.random(size=len(param_bounds))
                r2 = np.random.random(size=len(param_bounds))
                A2 = 2 * a * r1 - a
                C2 = 2 * r2
                D_beta = np.abs(C2 * beta_pos - population[i])
                X2 = beta_pos - A2 * D_beta
                
                r1 = np.random.random(size=len(param_bounds))
                r2 = np.random.random(size=len(param_bounds))
                A3 = 2 * a * r1 - a
                C3 = 2 * r2
                D_delta = np.abs(C3 * delta_pos - population[i])
                X3 = delta_pos - A3 * D_delta
                
                new_position = (X1 + X2 + X3) / 3.0
                
                # Aplicar limites
                for j, (param, (low, high)) in enumerate(param_bounds.items()):
                    if isinstance(low, int) and isinstance(high, int):
                        new_position[j] = int(np.clip(new_position[j], low, high))
                    else:
                        new_position[j] = np.clip(new_position[j], low, high)
                
                population[i] = new_position
            
            # Avaliar nova população
            new_fitness, new_metrics = self.evaluate_population(objective_function, population, param_names)
            
            # Atualizar líderes
            for i in range(self.num_wolves):
                if new_fitness[i] < self.alpha_score:
                    self.delta_score = self.beta_score
                    self.delta_idx = self.beta_idx
                    self.beta_score = self.alpha_score
                    self.beta_idx = self.alpha_idx
                    self.alpha_score = new_fitness[i]
                    self.alpha_idx = i
                elif new_fitness[i] < self.beta_score:
                    self.delta_score = self.beta_score
                    self.delta_idx = self.beta_idx
                    self.beta_score = new_fitness[i]
                    self.beta_idx = i
                elif new_fitness[i] < self.delta_score:
                    self.delta_score = new_fitness[i]
                    self.delta_idx = i
            
            # Relatório de progresso
            print(f"Iteração {iter+1}/{self.max_iter} | Melhor Fitness: {self.alpha_score:.4f}")
        
        # Recuperar melhor solução
        best_params = {
            param: population[self.alpha_idx, j]
            for j, param in enumerate(param_names)
        }
        
        return best_params, self.alpha_score, metrics_list[self.alpha_idx]
import numpy as np
import concurrent.futures
from collections import defaultdict

class GreyWolfOptimizer:
    def __init__(self, num_wolves=15, max_iter=50, num_objectives=5):
        self.num_wolves = num_wolves
        self.max_iter = max_iter
        self.num_objectives = num_objectives
        
        # Armazenar índices em vez de cópias dos objetos
        self.alpha_idx = None
        self.beta_idx = None
        self.delta_idx = None
        self.alpha_score = np.full(num_objectives, float('inf'))
        self.beta_score = np.full(num_objectives, float('inf'))
        self.delta_score = np.full(num_objectives, float('inf'))
        
    def initialize_population(self, param_ranges):
        population = []
        for _ in range(self.num_wolves):
            wolf = {}
            for param, (low, high) in param_ranges.items():
                if isinstance(low, int) and isinstance(high, int):
                    wolf[param] = np.random.randint(low, high + 1)
                else:
                    wolf[param] = np.random.uniform(low, high)
            population.append(wolf)
        return population
    
    def dominance(self, obj1, obj2):
        """Verifica se obj1 domina obj2 (minimização)"""
        not_worse = all(o1 <= o2 for o1, o2 in zip(obj1, obj2))
        better = any(o1 < o2 for o1, o2 in zip(obj1, obj2))
        return not_worse and better
    
    def update_leaders(self, population, scores):
        # Encontrar soluções não-dominadas
        non_dominated_idxs = []
        for i, score_i in enumerate(scores):
            dominated = False
            for j, score_j in enumerate(scores):
                if i != j and self.dominance(score_j, score_i):
                    dominated = True
                    break
            if not dominated:
                non_dominated_idxs.append(i)
        
        # Selecionar líderes Alpha, Beta, Delta
        if non_dominated_idxs:
            # Ordenar pelo primeiro objetivo
            non_dominated_idxs.sort(key=lambda i: scores[i][0])
            
            self.alpha_idx = non_dominated_idxs[0]
            self.alpha_score = scores[self.alpha_idx]
            
            if len(non_dominated_idxs) > 1:
                self.beta_idx = non_dominated_idxs[1]
                self.beta_score = scores[self.beta_idx]
            if len(non_dominated_idxs) > 2:
                self.delta_idx = non_dominated_idxs[2]
                self.delta_score = scores[self.delta_idx]
    
    def evaluate_population(self, objective_function, population):
        """Avalia população em paralelo usando threads"""
        with concurrent.futures.ThreadPoolExecutor() as executor:
            futures = [executor.submit(objective_function, wolf) for wolf in population]
            return [future.result() for future in concurrent.futures.as_completed(futures)]
    
    def run(self, objective_function, param_ranges):
        # Inicializar população
        population = self.initialize_population(param_ranges)
        
        # Avaliar população inicial em paralelo
        scores = self.evaluate_population(objective_function, population)
        
        # Definir líderes iniciais
        self.update_leaders(population, scores)
        
        # Loop de otimização
        for iter in range(self.max_iter):
            a = 2 - iter * (2 / self.max_iter)  # Coeficiente de decaimento
            
            # Atualizar posições
            for i in range(self.num_wolves):
                for param, (low, high) in param_ranges.items():
                    # Obter valores dos líderes
                    alpha_val = population[self.alpha_idx][param]
                    beta_val = population[self.beta_idx][param]
                    delta_val = population[self.delta_idx][param] if self.delta_idx is not None else 0
                    
                    # Coeficientes para Alpha
                    r1 = np.random.random()
                    r2 = np.random.random()
                    A1 = 2 * a * r1 - a
                    C1 = 2 * r2
                    D_alpha = abs(C1 * alpha_val - population[i][param])
                    X1 = alpha_val - A1 * D_alpha
                    
                    # Coeficientes para Beta
                    r1 = np.random.random()
                    r2 = np.random.random()
                    A2 = 2 * a * r1 - a
                    C2 = 2 * r2
                    D_beta = abs(C2 * beta_val - population[i][param])
                    X2 = beta_val - A2 * D_beta
                    
                    # Coeficientes para Delta (se existir)
                    if self.delta_idx is not None:
                        r1 = np.random.random()
                        r2 = np.random.random()
                        A3 = 2 * a * r1 - a
                        C3 = 2 * r2
                        D_delta = abs(C3 * delta_val - population[i][param])
                        X3 = delta_val - A3 * D_delta
                    else:
                        X3 = 0
                    
                    # Nova posição (média ponderada)
                    if self.delta_idx is not None:
                        new_position = (X1 + X2 + X3) / 3.0
                    else:
                        new_position = (X1 + X2) / 2.0
                    
                    # Garantir que está dentro dos limites
                    if isinstance(low, int) and isinstance(high, int):
                        new_position = int(np.clip(new_position, low, high))
                    else:
                        new_position = np.clip(new_position, low, high)
                    
                    population[i][param] = new_position
            
            # Avaliar nova população em paralelo
            new_scores = self.evaluate_population(objective_function, population)
            
            # Atualizar scores e líderes
            updated = False
            for i in range(self.num_wolves):
                if self.dominance(new_scores[i], scores[i]):
                    scores[i] = new_scores[i]
                    updated = True
            
            if updated:
                self.update_leaders(population, scores)
            
            # Relatório de progresso
            print(f"Iteração {iter+1}/{self.max_iter} | "
                  f"Alpha: {np.round(self.alpha_score, 4)} | "
                  f"Objetivos: {' '.join(f'[{i}]' for i in range(self.num_objectives))}")
        
        # Retornar o melhor lobo (alpha)
        return population[self.alpha_idx], self.alpha_score
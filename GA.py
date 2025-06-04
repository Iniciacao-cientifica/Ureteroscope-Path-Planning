import numpy as np
import random
from tqdm.auto import tqdm
from concurrent.futures import ThreadPoolExecutor

class GeneticAlgorithm:
    def __init__(self, objective_function, param_ranges, 
                 population_size=50, generations=100,
                 crossover_rate=0.8, mutation_rate=0.2,
                 elitism_count=2, tournament_size=5):
        """
        Inicializa o otimizador genético
        
        :param objective_function: Função objetivo que retorna fitness
        :param param_ranges: Dicionário com limites para cada parâmetro
        :param population_size: Tamanho da população
        :param generations: Número de gerações
        :param crossover_rate: Probabilidade de crossover
        :param mutation_rate: Probabilidade de mutação
        :param elitism_count: Número de melhores indivíduos para preservar
        :param tournament_size: Tamanho do torneio para seleção
        """
        self.objective_function = objective_function
        self.param_ranges = param_ranges
        self.param_names = list(param_ranges.keys())
        self.population_size = population_size
        self.generations = generations
        self.crossover_rate = crossover_rate
        self.mutation_rate = mutation_rate
        self.elitism_count = elitism_count
        self.tournament_size = tournament_size
        
        # Histórico
        self.best_fitness_history = []
        self.best_params_history = []
        
    def initialize_population(self):
        """Cria população inicial aleatória"""
        population = []
        for _ in range(self.population_size):
            individual = {}
            for param, (low, high) in self.param_ranges.items():
                if isinstance(low, int) and isinstance(high, int):
                    individual[param] = random.randint(low, high)
                else:
                    individual[param] = random.uniform(low, high)
            population.append(individual)
        return population
    
    def evaluate_individual(self, individual):
        """Avalia um indivíduo usando a função objetivo"""
        return self.objective_function(individual)
    
    def evaluate_population(self, population):
        """Avalia toda a população em paralelo"""
        with ThreadPoolExecutor() as executor:
            results = list(executor.map(self.evaluate_individual, population))
        return results
    
    def select_parents(self, population, fitness_scores):
        """Seleção de pais usando torneio"""
        parents = []
        for _ in range(2):  # Selecionar 2 pais
            tournament_indices = random.sample(range(len(population)), self.tournament_size)
            tournament_fitness = [fitness_scores[i] for i in tournament_indices]
            winner_idx = tournament_indices[np.argmin(tournament_fitness)]  # Minimização
            parents.append(population[winner_idx])
        return parents
    
    def crossover(self, parent1, parent2):
        """Crossover de um ponto"""
        child = {}
        crossover_point = random.randint(1, len(self.param_names) - 1)
        
        for i, param in enumerate(self.param_names):
            if i < crossover_point:
                child[param] = parent1[param]
            else:
                child[param] = parent2[param]
                
        return child
    
    def mutate(self, individual):
        """Aplica mutação a um indivíduo"""
        mutated = individual.copy()
        for param in self.param_names:
            if random.random() < self.mutation_rate:
                low, high = self.param_ranges[param]
                if isinstance(low, int) and isinstance(high, int):
                    mutated[param] = random.randint(low, high)
                else:
                    mutated[param] = random.uniform(low, high)
        return mutated
    
    def run(self):
        """Executa a otimização genética"""
        population = self.initialize_population()
        best_individual = None
        best_fitness = float('inf')
        
        progress_bar = tqdm(total=self.generations, desc="Genetic Algorithm")
        
        for gen in range(self.generations):
            # Avaliar população
            fitness_scores = self.evaluate_population(population)
            
            # Encontrar melhor indivíduo
            min_fitness_idx = np.argmin(fitness_scores)
            current_best_fitness = fitness_scores[min_fitness_idx]
            current_best_individual = population[min_fitness_idx]
            
            # Atualizar melhor global
            if current_best_fitness < best_fitness:
                best_fitness = current_best_fitness
                best_individual = current_best_individual.copy()
            
            # Manter histórico
            self.best_fitness_history.append(best_fitness)
            self.best_params_history.append(best_individual)
            
            # Criar nova população
            new_population = []
            
            # Elitismo: preservar os melhores indivíduos
            elite_indices = np.argsort(fitness_scores)[:self.elitism_count]
            for idx in elite_indices:
                new_population.append(population[idx])
            
            # Preencher o restante da população
            while len(new_population) < self.population_size:
                # Selecionar pais
                parent1, parent2 = self.select_parents(population, fitness_scores)
                
                # Crossover
                if random.random() < self.crossover_rate:
                    child = self.crossover(parent1, parent2)
                else:
                    child = random.choice([parent1, parent2])
                
                # Mutação
                child = self.mutate(child)
                new_population.append(child)
            
            population = new_population
            
            # Atualizar barra de progresso
            progress_bar.set_postfix({
                "Fitness": best_fitness,
                "CurrBest": current_best_fitness
            })
            progress_bar.update(1)
        
        progress_bar.close()
        return best_individual, best_fitness
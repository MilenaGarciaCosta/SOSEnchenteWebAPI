# SOS Enchente Web API
O projeto é uma API REST desenvolvida para o projeto **SOS Enchente**, uma solução voltada ao apoio de usuários em situações de enchentes e emergências.
A API é responsável por disponibilizar os serviços de backend da aplicação, incluindo gerenciamento de usuários, autenticação, perfis de saúde, contatos de emergência, dependentes, prioridades e comunicação de alertas em tempo real.
O projeto foi desenvolvido como parte da **Global Solution da FIAP**.

## Equipe
| Integrante | RM |
|---|---:|
| Vinicius Vilas Boas | 557843 |
| Milena Costa | 555111 |
| Renan Gonçalves | 555584 |
| Gustavo Luni | 558096 |

## Objetivo
O SOS Enchente busca oferecer uma solução tecnológica para auxiliar pessoas durante situações de enchentes e outros cenários de emergência.
O backend centraliza informações do usuário e disponibiliza serviços que podem ser consumidos pela aplicação mobile, permitindo:

- Cadastro e autenticação de usuários;
- Login com autenticação baseada em JWT;
- Gerenciamento de dados pessoais;
- Cadastro de perfil de saúde;
- Cadastro de contatos de emergência;
- Cadastro de dependentes;
- Definição e utilização de prioridades;
- Validação de dados recebidos pela API;
- Comunicação de alertas em tempo real;
- Integração com MQTT para recebimento de eventos;
- Persistência dos dados em banco MySQL.

### Comunicação em tempo real
- SignalR
- MQTT
- MQTTnet

### Testes
- Projeto `SOSEnchenteWebAPI.Tests`

# Arquitetura
A aplicação utiliza uma organização em camadas, separando responsabilidades entre controllers, serviços, domínio e acesso aos dados.

```text
SOSEnchenteWebAPI/
│
├── SOSEnchenteWebAPI/
│   │
│   ├── Controllers/
│   │   └── Controllers da API
│   │
│   ├── DbAdapter/
│   │   ├── AppDbContext.cs
│   │   └── Repositories/
│   │
│   ├── Domain/
│   │   ├── DTOs/
│   │   ├── Interfaces/
│   │   └── Models/
│   │
│   ├── Services/
│   │   ├── PasswordService
│   │   ├── JwtService
│   │   ├── ValidationService
│   │   └── PriorityService
│   │
│   ├── Hubs/
│   │   └── AlertHub.cs
│   │
│   ├── Program.cs
│   └── arquivos de configuração
│
├── SOSEnchenteWebAPI.Tests/
│   └── Testes automatizados
│
└── SOSEnchenteWebAPI.slnx
```

# Componentes

## Controllers
Os controllers são responsáveis por receber as requisições HTTP, validar os dados de entrada, chamar os serviços ou repositórios correspondentes e retornar as respostas HTTP.
O controller de usuários utiliza a rota base:
```text
/api/user
```
e concentra operações relacionadas à autenticação e aos dados do usuário.

## Domain
Essa camada contém os principais elementos de domínio da aplicação:

- Models;
- DTOs;
- Interfaces;
- Contratos utilizados pelos serviços e repositórios.

Essa separação permite que as regras de negócio e os modelos utilizados pela aplicação não fiquem diretamente acoplados às requisições HTTP.

## DbAdapter
A camada `DbAdapter` concentra o acesso ao banco de dados.
O `AppDbContext` utiliza Entity Framework Core e possui entidades relacionadas a:

- `User`;
- `HealthProfile`;
- `EmergencyContact`;
- `Dependent`.

Também são configurados relacionamentos entre essas entidades, incluindo relacionamento um-para-um entre usuário e perfil de saúde, relacionamento um-para-muitos entre usuário e contatos de emergência e uma relação de dependência entre usuários.

## Services
A aplicação possui serviços específicos para responsabilidades como:

- Criação e validação de senhas;
- Geração de tokens JWT;
- Validação de e-mail e telefone;
- Regras relacionadas a prioridades;
- Outras regras de negócio.

# Autenticação
A API utiliza **JWT Bearer Authentication**.
Depois de realizar o cadastro ou login com sucesso, a API retorna um token JWT que deve ser enviado nas requisições que exigem autenticação.

O formato esperado é...

```http
Authorization: Bearer SEU_TOKEN
```

Endpoints protegidos utilizam o atributo...

```csharp
[Authorize]
```

# Endpoints

## Usuários
### Registrar usuário

```http
POST /api/user/register
```

Cadastra um novo usuário e retorna um token JWT após o registro.

### Exemplo de requisição

```json
{
  "email": "usuario@email.com",
  "password": "Senha123!",
  "name": "Nome do Usuário",
  "age": 25,
  "phoneNumber": "11999999999",
  "healthProfile": {
    "mobilityLevel": 1,
    "livesAlone": false,
    "healthConditions": ""
  }
}
```

## Login

```http
POST /api/user/login
```

Autentica um usuário existente.

### Exemplo
```json
{
  "email": "usuario@email.com",
  "password": "Senha123!"
}
```

### Resposta de sucesso

```json
{
  "success": true,
  "message": "Sucesso ao fazer login.",
  "token": "JWT_TOKEN",
  "user": {
    "id": 1,
    "email": "usuario@email.com",
    "name": "Nome do Usuário",
    "age": 25,
    "phoneNumber": "11999999999"
  }
}
```

## Buscar usuário autenticado

```http
GET /api/user
```

Requer autenticação JWT.

Retorna os dados do usuário associado ao token utilizado na requisição.

### Header

```http
Authorization: Bearer SEU_TOKEN
```

## Atualizar usuário

```http
PUT /api/user
```

Requer autenticação JWT.

Atualiza os dados do usuário autenticado.

### Exemplo

```json
{
  "name": "Nome Atualizado",
  "age": 26,
  "phoneNumber": "11988888888",
  "healthProfile": {
    "mobilityLevel": 2,
    "livesAlone": true,
    "healthConditions": "Nenhuma"
  }
}
```

### Validações

A API verifica:

- Nome obrigatório;
- Idade entre 1 e 120 anos;
- Número de telefone válido, quando informado;
- Nível de mobilidade entre 1 e 4.

# Contatos de emergência

Os contatos de emergência são associados ao usuário autenticado.

## Listar contatos

```http
GET /api/user/contacts
```

Requer autenticação JWT.

### Resposta

Retorna uma lista de contatos cadastrados para o usuário.

Exemplo:

```json
[
  {
    "id": 1,
    "name": "Maria",
    "description": "Mãe",
    "phoneNumber": "11999999999"
  }
]
```

## Adicionar contato

```http
POST /api/user/contacts
```

Requer autenticação JWT.

### Exemplo

```json
{
  "name": "Maria",
  "description": "Mãe",
  "phoneNumber": "11999999999"
}
```

### Validações

- Nome obrigatório;
- Número de telefone obrigatório;
- Número de telefone deve ser válido.

# Dependentes

A API permite associar outros usuários como dependentes.

## Listar dependentes

```http
GET /api/user/dependents
```

Requer autenticação JWT.

### Resposta

```json
[
  {
    "id": 2,
    "name": "Usuário Dependente",
    "age": 70
  }
]
```

## Adicionar dependente

```http
POST /api/user/dependents
```

Requer autenticação JWT.

O dependente é localizado através do endereço de e-mail.

### Exemplo

```json
{
  "dependentEmail": "dependente@email.com"
}
```

### Regras

A API impede:

- Usuário adicionar a si próprio como dependente;
- Cadastro de dependente inexistente;
- Requisição sem usuário autenticado.

# Banco de dados

A persistência é realizada utilizando:

```text
Entity Framework Core
        |
        v
Pomelo.EntityFrameworkCore.MySql
        |
        v
      MySQL
```

O `AppDbContext` possui as seguintes entidades:

```csharp
public DbSet<User> Users { get; set; }

public DbSet<HealthProfile> HealthProfiles { get; set; }

public DbSet<EmergencyContact> EmergencyContacts { get; set; }

public DbSet<Dependent> Dependents { get; set; }
```

### Relacionamentos

```text
User
 |
 +-- HealthProfile
 |
 +-- EmergencyContacts
 |
 +-- Dependents
```

O perfil de saúde possui relacionamento 1:1 com o usuário.

Os contatos de emergência possuem relacionamento 1:n com o usuário.

A entidade `Dependent` utiliza uma PK composta por...

```text
UserId + DependentId
```

O usuário principal possui exclusão em cascata para seus dados relacionados, enquanto a referência ao usuário dependente utiliza comportamento `Restrict`.

# Comunicação em tempo real
O projeto utiliza **ASP.NET Core SignalR** para comunicação em tempo real.

O Hub principal é:

```text
/hubs/alerts
```

A classe responsável pelo Hub é:

```text
Hubs/AlertHub.cs
```

```csharp
public class AlertHub : Hub
{
}
```

O Hub é registrado na aplicação através de:

```csharp
app.MapHub<AlertHub>("/hubs/alerts");
```

# MQTT

A aplicação também possui integração com MQTT através do pacote:

```text
MQTTnet
```

O `Program.cs` registra um serviço hospedado chamado:

```text
MqttAlertListener
```

```csharp
builder.Services.AddHostedService<MqttAlertListener>();
```

A finalidade da integração é permitir que eventos relacionados a alertas possam ser recebidos através de um broker MQTT e posteriormente processados pelo backend.

# Estrutura de autenticação

O fluxo de autenticação funciona da seguinte maneira:

```text
                 POST /api/user/register
                           |
                           v
                     Validação
                           |
                           v
                  Hash da senha
                           |
                           v
                    Banco de dados
                           |
                           v
                    Geração JWT
                           |
                           v
                    Token retornado
```

Para login:

```text
                 POST /api/user/login
                           |
                           v
                  Busca pelo e-mail
                           |
                           v
                  Verificação BCrypt
                           |
                           v
                    Geração JWT
                           |
                           v
                    Token retornado
```

Nas requisições protegidas:

```text
Cliente
   |
   | Authorization: Bearer JWT
   v
ASP.NET Core
   |
   v
Validação do token
   |
   +---- inválido ----> 401 Unauthorized
   |
   v
Controller
   |
   v
Serviço / Repository
   |
   v
Banco de dados
```

# Validação

A API possui um serviço dedicado para validação:

```text
IValidationService
ValidationService
```

Entre as validações utilizadas pelos controllers estão:

- E-mail;
- Telefone;
- Idade;
- Nível de mobilidade;
- Campos obrigatórios.

# Testes
O repositório possui um projeto separado para testes...

```text
SOSEnchenteWebAPI.Tests
```

Estrutura geral:

```text
SOSEnchenteWebAPI.slnx
│
├── SOSEnchenteWebAPI
│   └── Aplicação
│
└── SOSEnchenteWebAPI.Tests
    └── Testes
```

Para executar os testes no CLI

```bash
dotnet test
```

Para executar a partir da solução

```bash
dotnet test SOSEnchenteWebAPI.slnx
```

# Como executar o projeto

## Pré-requisitos

Antes de executar a aplicação, é necessário possuir:

- .NET SDK compatível com o projeto;
- MySQL;
- Git;
- IDE ou terminal compatível com projetos .NET.

A versão do framework configurada no projeto é:

```text
net10.0
```

## Clonar o repositório
```bash
git clone https://github.com/MilenaGarciaCosta/SOSEnchenteWebAPI.git
```

Entrar no diretório:
```bash
cd SOSEnchenteWebAPI
```

## Compilar
```bash
dotnet build
```

## Executar os testes
```bash
dotnet test
```

## Executar a API
```bash
dotnet run --project SOSEnchenteWebAPI
```

# Configuração do banco de dados
A aplicação utiliza uma connection string denominada:

```text
DefaultConnection
```

```csharp
builder.Configuration.GetConnectionString("DefaultConnection")
```

Antes de iniciar a API, configure corretamente:

- Servidor MySQL;
- Porta;
- Nome do banco;
- Usuário;
- Senha;
- Demais parâmetros da connection string.

Execute as **Migrations** com o seguinte comando...
```csharp
dotnet ef database update -p 'SOSEnchenteWebAPI' -c 'AppDbContext'
```

# Resumo dos endpoints

| Método | Endpoint | Autenticação | Descrição |
|---|---|---|---|
| POST | `/api/user/register` | Não | Cadastra um usuário |
| POST | `/api/user/login` | Não | Realiza login e gera JWT |
| GET | `/api/user` | Sim | Consulta o usuário autenticado |
| PUT | `/api/user` | Sim | Atualiza o usuário autenticado |
| GET | `/api/user/contacts` | Sim | Lista contatos de emergência |
| POST | `/api/user/contacts` | Sim | Adiciona contato de emergência |
| GET | `/api/user/dependents` | Sim | Lista dependentes |
| POST | `/api/user/dependents` | Sim | Adiciona dependente |
| GET | `/hubs/alerts` | SignalR | Hub de comunicação de alertas |

# Fluxo de comunicação
A arquitetura pode ser representada de forma simplificada como:

```text
                   Aplicação Mobile
                         |
                         | HTTP / REST
                         v
                +-------------------+
                |   ASP.NET Core    |
                |      Web API      |
                +-------------------+
                    |       |      |
                    |       |      |
                    v       v      v
              Services  SignalR   MQTT
                    |
                    v
              Repositories
                    |
                    v
              Entity Framework
                    |
                    v
                  MySQL
```

# Documentação

A documentação interativa da API está disponível através do Swagger quando a aplicação estiver em execução:

```text
https://localhost:7272/swagger/index.html
```

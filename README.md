# Gelişim Pusulası | AI-Powered Employee Development

**360-Degree Performance Evaluation • Personalized Development Plans • AI Mentor**

An industry-oriented undergraduate graduation project (2026) developed by **Yağmur Ateş** and **Şevval Gül**, Department of Industrial Engineering, Bursa Technical University, within the **TUSAŞ LIFT UP** program.

> \*\*Project status:\*\* Academic research prototype, evaluated with synthetic data. This repository is not a production-ready HR system or an official TUSAŞ product.

## Overview

Gelişim Pusulası is a Windows desktop prototype that transforms multi-source (360-degree) performance evaluation results into actionable, employee-specific development plans. It combines competency-based scoring, large language model (LLM) assistance, interactive mentoring, reporting, and development-progress tracking in a role-based application.

The project investigates how to make performance feedback more actionable while considering privacy and deployment constraints in sensitive organizational environments.

## Key Features

* **360-degree evaluation:** A competency framework of **7 competencies × 3 behavioral indicators (21 items)**, with multi-source weighted scoring and development-level mapping.
* **Role-based interfaces:** Employee, manager, and HR views, including evaluation tasks, personal reports, team reports, and an HR analytics dashboard.
* **Personalized development reports:** LLM-supported analysis of strengths and improvement areas, with actionable development suggestions.
* **AI Mentor:** Interactive, context-aware guidance for employees working on their development plans.
* **Development journey:** Training/action cards, completion tracking, employee reflections, and comparison-oriented reporting across evaluation periods.
* **Automated reminders:** Email reminder functionality for inactive development steps.
* **AI usage observability:** Provider/model selection, latency and token tracking, estimated cost calculation, and usage logging.

## Technology Stack

|Layer|Technologies|
|-|-|
|Desktop application|C#, Windows Forms, .NET Framework 4.7.2|
|Data storage|Microsoft SQL Server|
|AI integration|OpenRouter and Ollama integration|
|LLM evaluation|Qwen2.5, Gemma 4 E2B, gpt-oss-120b, GLM-5.1|
|Development environment|Visual Studio, NuGet|

## Architecture

The prototype follows a layered structure:

1. **Presentation layer:** Windows Forms interfaces for employees, managers, and HR.
2. **Application layer:** Evaluation/scoring, development plans, AI gateway, reports, and reminders.
3. **Data layer:** SQL Server for application data and AI usage information.
4. **External services:** Configurable local/cloud LLM providers and email delivery.

## Research and Evaluation

All end-to-end prototype tests described in the graduation study used a **synthetic dataset of 100 employees**, rather than real employee performance data.

Four models were compared for report generation and AI Mentor interactions:

|Evaluation component|Study design|
|-|-|
|Development report generation|**180 reports**: 4 models × 15 employees × 3 runs|
|AI Mentor|**36 conversation outputs**: 3 employees × 4 models × 3 turns|
|Dimensions|Output quality, response time, and data-security considerations|
|Assessment|Six-dimension LLM-based rubric, with a subset compared against human ratings|

In the reported experiments, **GLM-5.1** achieved the highest overall quality scores for report generation and mentor dialogue, while **gpt-oss-120b** provided faster responses. These findings are specific to the study's tasks, evaluation setup, and tested models; they are not a general model ranking.

!\[Model response-time comparison](Grafikler/01\_ortalama\_toplam\_yanit\_suresi.png)

## Repository Structure

* `AI/` — AI gateway, provider integrations, scoring and development-support services.
* `\*.cs`, `\*.Designer.cs`, `\*.resx` — Windows Forms application code and UI resources.
* `Grafikler/` — Selected research visualizations.
* `Properties/` and `Resources/` — Project settings and application resources.
* `WindowsFormsApp1.sln`, `WindowsFormsApp1.csproj` — Visual Studio solution and project files.
* `App.config` — Example local SQL Server connection settings (no passwords should be committed).

## Running the Prototype

**Requirements:** Windows, Visual Studio with the .NET Framework 4.7.2 development tools, NuGet package restore, and a compatible Microsoft SQL Server instance.

1. Clone this repository: `git clone https://github.com/ys-devlab/gelisim-pusulasi.git`
2. Open `WindowsFormsApp1.sln` in Visual Studio and restore NuGet dependencies.
3. Configure the database connection in `App.config` for your local SQL Server instance.
4. Configure the AI provider through local environment variables or an untracked `ai.env` file. **Never commit real API keys or credentials.**

**Reproducibility note:** Database initialization scripts, synthetic data files, and certain experimental materials are not included in this repository snapshot. Consequently, the application may not run end-to-end from a fresh clone without those additional, approved resources. The repository currently serves primarily as a source-code and research portfolio.

## Limitations and Responsible Use

* This is a research prototype, **not** a validated production HR decision-making tool.
* Cross-period development tracking is implemented as a design/prototype feature; its effectiveness with real longitudinal organizational data has not been experimentally established.
* Any use involving actual employee data requires appropriate privacy, information-security, and organizational approvals.
* The LIFT UP project context does not imply official endorsement or production deployment by TUSAŞ.

## Authors

* **Yağmur Ateş** — Co-developer and undergraduate researcher
* **Şevval Gül** — Co-developer and undergraduate researcher

**Academic affiliation:** Bursa Technical University, Department of Industrial Engineering.

## Türkçe Özet

**Gelişim Pusulası**, 360 derece performans değerlendirme sonuçlarını analiz ederek çalışanlara kişiselleştirilmiş gelişim planları sunmak üzere geliştirilmiş, C# Windows Forms ve SQL Server tabanlı bir lisans bitirme projesidir. Sistem; yetkinlik değerlendirme, yapay zekâ destekli gelişim raporları, AI Mentor sohbeti, gelişim yolculuğu ve yönetici/İK raporlarını bir araya getirir.

Çalışma TUSAŞ **LIFT UP** programı kapsamında gerçekleştirilmiştir. Prototip testlerinde gerçek çalışan verileri yerine **100 kişilik sentetik veri seti** kullanılmış, dört büyük dil modeli rapor üretimi ve etkileşimli mentorluk görevlerinde karşılaştırılmıştır.

**Not:** Depoda yer almayan veritabanı kurulum dosyaları ve deney malzemeleri nedeniyle proje şu anda doğrudan çalıştırılabilir bir paket olarak sunulmamaktadır.

## License and Usage

No open-source license has been assigned to this repository. Please contact the project authors before redistributing or reusing the code or materials.



\## Application Screenshots



\### 1. Overview Dashboard

!\[Overview Dashboard](screenshots/overview.png)



\### 2. 360-Degree Evaluation

!\[360-Degree Evaluation](screenshots/evaluation.png)



\### 3. AI Mentor

!\[AI Mentor](screenshots/ai-mentor.png)



\### 4. Personal Development Journey

!\[Development Journey](screenshots/development-journey.png)



\### 5. HR Performance Analytics

!\[HR Dashboard](screenshots/hr-dashboard.png)


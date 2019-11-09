

Notes on deployment and setup:
- Azure Portal: Create ResGroup
- Azure Portal: Create KeyVault, update KeyVault name in appsettings.json
- Visual Studio: Deploy app: create profile with free tier plan, publish app
- Azure Portal - WebApp: Set App Identity: System assigned On, Save
- Azure Portal - KeyVault: Set KeyVault Access policy: Add, Template Secret Management, Select principal - find WebApp, Select, Add, Save
- Azure Portal - IoTHub: IoT devices, New, Enter Device ID, Save
- Azure Portal - IoTHub: Shared access policies - service, Copy Connection string-primary key
- WebApp - Configuration: Enter device name (same as ID), Enter IotHub connection string


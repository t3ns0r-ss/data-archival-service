# Data Archival Service - Design Documentation

## Overview
The Data Archival Service is a microservice designed to automatically archive data from a MySQL RDS database to a PostgreSQL archive database based on configurable criteria. The service provides REST APIs for managing archive configurations and viewing archived data with role-based access control.

## Architecture Overview

### High-Level Architecture
```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Source DB     │    │  Archival       │    │   Archive DB    │
│   (MySQL RDS)   │◄───┤   Service       ├───►│  (PostgreSQL)   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                               │
                               ▼
                       ┌─────────────────┐
                       │   REST API      │
                       │ (Role-based     │
                       │  Access)        │
                       └─────────────────┘
```

### Component Architecture
1. **Archive Configuration Manager**: Manages table-specific archival criteria
2. **Schema Discovery Service**: Discovers and mirrors table schemas
3. **Data Archival Engine**: Performs the actual data archival process
4. **Scheduler Service**: Runs archival tasks every 24 hours
5. **REST API Controller**: Provides endpoints for configuration and data access
6. **Authentication Service**: JWT-based authentication and role management
7. **Database Services**: Abstraction layer for MySQL and PostgreSQL operations

## Key Design Decisions

### 1. Database Choice
- **Source Database**: MySQL
- **Archive Database**: PostgreSQL
- **Rationale**: PostgreSQL provides excellent archival capabilities, JSONB support for flexible schema storage, and robust data integrity features.

### 2. Schema Mirroring Strategy
- **Approach**: Dynamic schema discovery and replication
- **Implementation**: Query MySQL INFORMATION_SCHEMA to get table structure and recreate in PostgreSQL
- **Benefits**: Ensures data integrity and maintains referential relationships

### 3. Archival Criteria
- **Based on**: `created_at` timestamp column
- **Flexibility**: Per-table configuration with different retention periods
- **Configuration Storage**: Dedicated `archive_config` table in PostgreSQL

### 4. Authentication & Authorization
- **Method**: JWT tokens with role-based access control
- **Roles**: Table-specific roles (e.g., "student") + "admin" role
- **Security**: Role validation against table access permissions

### 5. Scheduling Strategy
- **Frequency**: Every 24 hours
- **Implementation**: Background service with configurable intervals
- **Error Handling**: Comprehensive logging and retry mechanisms

### 6. Data Security Measures
- **Encryption**: Connection string encryption
- **Access Control**: Role-based API access
- **Audit Trail**: Complete logging of archival operations
- **Data Integrity**: Transaction-based archival with rollback capability

## Database Schema Design

### Archive Configuration Table
```sql
CREATE TABLE archive_config (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(255) NOT NULL UNIQUE,
    archive_after_days INTEGER NOT NULL,
    delete_after_days INTEGER,
    is_enabled BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Archive Log Table
```sql
CREATE TABLE archive_log (
    id SERIAL PRIMARY KEY,
    table_name VARCHAR(255) NOT NULL,
    records_archived INTEGER NOT NULL,
    records_deleted INTEGER DEFAULT 0,
    archive_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    status VARCHAR(50) NOT NULL,
    error_message TEXT
);
```

### User Roles Table
```sql
CREATE TABLE user_roles (
    id SERIAL PRIMARY KEY,
    username VARCHAR(255) NOT NULL,
    role VARCHAR(255) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(username, role)
);
```

## API Design

### Authentication Endpoints
- `POST /api/auth/login` - User authentication
- `POST /api/auth/refresh` - Token refresh

### Configuration Endpoints
- `GET /api/config` - List all archive configurations (admin only)
- `POST /api/config` - Create new archive configuration (admin only)
- `PUT /api/config/{tableName}` - Update archive configuration (admin only)
- `DELETE /api/config/{tableName}` - Delete archive configuration (admin only)

### Archive Data Endpoints
- `GET /api/archive/{tableName}` - View archived data for specific table
- `GET /api/archive/{tableName}/count` - Get count of archived records
- `GET /api/archive/logs` - View archival operation logs (admin only)

### Manual Operations
- `POST /api/archive/run/{tableName}` - Manual archival trigger (admin only)
- `POST /api/archive/run-all` - Archive all configured tables (admin only)

## Security Implementation

### JWT Token Structure
```json
{
  "sub": "username",
  "roles": ["student", "admin"],
  "exp": 1234567890,
  "iat": 1234567890
}
```

### Role-Based Access Control
- **Table Access**: Users can only access archived data for tables matching their roles
- **Admin Access**: Admin role can access all tables and configurations
- **API Security**: All endpoints require valid JWT tokens

## Deployment Strategy

### Docker Containerization
- **Base Image**: `mcr.microsoft.com/dotnet/aspnet:8.0`
- **Multi-stage Build**: Separate build and runtime stages
- **Configuration**: Environment variables for database connections
- **Health Checks**: Built-in health check endpoints

### Cross-Platform Compatibility
- **.NET 8**: Runs on any Linux-based system
- **Database Drivers**: Native .NET PostgreSQL and MySQL providers
- **Configuration**: Flexible configuration through appsettings.json and environment variables

## Monitoring and Logging

### Logging Strategy
- **Structured Logging**: JSON-formatted logs with correlation IDs
- **Log Levels**: Debug, Info, Warning, Error, Critical
- **Audit Trail**: All archival operations logged with timestamps and results

### Health Monitoring
- **Database Connectivity**: Health checks for both source and archive databases
- **Service Status**: API endpoint health checks
- **Scheduled Jobs**: Job execution status monitoring

## Error Handling and Recovery

### Archival Process Error Handling
1. **Transaction Rollback**: Failed archival operations are rolled back
2. **Retry Logic**: Configurable retry attempts with exponential backoff
3. **Error Logging**: Detailed error logging with stack traces
4. **Notification**: Critical errors can trigger notifications (configurable)

### Data Consistency
- **Atomic Operations**: All archival operations are transactional
- **Verification**: Post-archival data verification
- **Backup Strategy**: Archive database backup recommendations

## Performance Considerations

### Batch Processing
- **Chunk Size**: Configurable batch sizes for large table archival
- **Memory Management**: Streaming data processing to minimize memory usage
- **Connection Pooling**: Efficient database connection management

### Indexing Strategy
- **Archive Tables**: Automatic indexing on `created_at` and primary key columns
- **Query Optimization**: Optimized queries for both archival and retrieval operations

## Future Enhancements

### Phase 2 Features
1. **Multiple Archive Destinations**: Support for multiple archive databases
2. **Compression**: Data compression for long-term storage
3. **Encryption**: Data-at-rest encryption for sensitive information

### Scalability Improvements
1. **Horizontal Scaling**: Support for multiple service instances
2. **Queue-based Processing**: Message queue integration for large-scale operations
3. **Distributed Locking**: Coordination between multiple service instances

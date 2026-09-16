const { Pool } = require("pg");

// Variables de entorno para configuración, nunca credenciales hardcodeadas (sección 8
// del enunciado). DATABASE_URL la inyecta docker-compose.
const pool = new Pool({
  connectionString: process.env.DATABASE_URL,
});

module.exports = { pool };

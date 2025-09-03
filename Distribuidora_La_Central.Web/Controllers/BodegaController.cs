using Distribuidora_La_Central.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Data;
using System.Data.SqlClient;

namespace Distribuidora_La_Central.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BodegaController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // 🔹 Constantes
        private const string ConnectionName = "DefaultConnection";
        private const string TablaBodega = "Bodega";

        // Mensajes
        private const string MsgNoBodegas = "No se encontraron bodegas.";
        private const string MsgBodegaExiste = "La bodega ya existe";
        private const string MsgBodegaRegistrada = "Bodega registrada exitosamente";
        private const string MsgErrorRegistrar = "Error al registrar bodega";

        // Parámetros
        private const string ParamNombre = "@nombre";
        private const string ParamUbicacion = "@ubicacion";
        private const string ParamResponsable = "@responsable";
        private const string ParamFecha = "@fecha";

        public BodegaController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("obtener-todos")]
        public string ObtenerTodasBodegas()
        {
            using SqlConnection con = new(_configuration.GetConnectionString(ConnectionName));
            SqlDataAdapter da = new($"SELECT * FROM {TablaBodega}", con);
            DataTable dt = new DataTable();
            da.Fill(dt);

            List<Bodega> bodegaList = new List<Bodega>();
            Response response = new Response();

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    Bodega bodega = new Bodega
                    {
                        idBodega = Convert.ToInt32(row["idBodega"]),
                        nombre = Convert.ToString(row["nombre"]),
                        ubicacion = Convert.ToString(row["ubicacion"]),
                        responsable = row["responsable"] == DBNull.Value ? 0 : Convert.ToInt32(row["responsable"]),
                        fecha = row["fecha"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(row["fecha"])
                    };

                    bodegaList.Add(bodega);
                }
            }

            if (bodegaList.Count > 0)
                return JsonConvert.SerializeObject(bodegaList);

            response.StatusCode = 100;
            response.ErrorMessage = MsgNoBodegas;
            return JsonConvert.SerializeObject(response);
        }

        [HttpPost("registrar")]
        public IActionResult Registrar([FromBody] Bodega bodega)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));

            // Verificar si ya existe una bodega con el mismo nombre
            SqlDataAdapter checkBodega = new SqlDataAdapter($"SELECT * FROM {TablaBodega} WHERE nombre = {ParamNombre}", con);
            checkBodega.SelectCommand.Parameters.AddWithValue(ParamNombre, bodega.nombre);

            DataTable dt = new DataTable();
            checkBodega.Fill(dt);

            if (dt.Rows.Count > 0)
                return BadRequest(MsgBodegaExiste);

            // Insertar la nueva bodega
            SqlCommand cmd = new SqlCommand($@"
                INSERT INTO {TablaBodega} (nombre, ubicacion, responsable, fecha) 
                VALUES ({ParamNombre}, {ParamUbicacion}, {ParamResponsable}, {ParamFecha})", con);

            cmd.Parameters.AddWithValue(ParamNombre, bodega.nombre);
            cmd.Parameters.AddWithValue(ParamUbicacion, bodega.ubicacion);
            cmd.Parameters.AddWithValue(ParamResponsable, bodega.responsable);
            cmd.Parameters.AddWithValue(ParamFecha, bodega.fecha);

            con.Open();
            int i = cmd.ExecuteNonQuery();
            con.Close();

            if (i > 0)
                return Ok(MsgBodegaRegistrada);

            return StatusCode(500, MsgErrorRegistrar);
        }
    }
}

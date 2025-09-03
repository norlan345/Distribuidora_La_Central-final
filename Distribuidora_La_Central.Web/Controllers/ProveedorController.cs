using Distribuidora_La_Central.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Data;
using System.Data.SqlClient;
using static Distribuidora_La_Central.Shared.Pages.GestionUsuarios;

namespace Distribuidora_La_Central.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedorController : ControllerBase
    {
        public readonly IConfiguration _configuration;
        public ProveedorController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("obtener-todos")]
        public string ObtenerTodosProveedores()
        {
            using SqlConnection con = new(_configuration.GetConnectionString("DefaultConnection"));
            SqlDataAdapter da = new("SELECT * FROM Proveedor", con);
            DataTable dt = new DataTable();
            da.Fill(dt);

            List<Proveedor> proveedorList = new List<Proveedor>();
            Response response = new Response();

            if (dt.Rows.Count > 0)
            {
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    Proveedor proveedor = new Proveedor();
                    proveedor.idProveedor = Convert.ToInt32(dt.Rows[i]["idProveedor"]);
                    proveedor.nombre = Convert.ToString(dt.Rows[i]["nombre"]);
                    proveedor.razonSocial = Convert.ToString(dt.Rows[i]["razonSocial"]);
                    proveedor.contacto = Convert.ToString(dt.Rows[i]["contacto"]);
                    proveedor.telefono = Convert.ToString(dt.Rows[i]["telefono"]);
                    proveedor.diaIngreso = Convert.ToDateTime(dt.Rows[i]["diaIngreso"]);
                    proveedorList.Add(proveedor);
                }
            }

            if (proveedorList.Count > 0)
                return JsonConvert.SerializeObject(proveedorList);
            else
            {
                response.StatusCode = 100;
                response.ErrorMessage = "No se encontraron proveedores.";
                return JsonConvert.SerializeObject(response);
            }

        }

        [HttpPost("registrar")]
        public IActionResult Registrar([FromBody] Proveedor proveedor)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));

            // Verificar si ya existe un proveedor con el mismo nombre
            SqlDataAdapter checkProveedor = new SqlDataAdapter("SELECT * FROM Proveedor WHERE nombre = @nombre", con);
            checkProveedor.SelectCommand.Parameters.AddWithValue("@nombre", proveedor.nombre);

            DataTable dt = new DataTable();
            checkProveedor.Fill(dt);

            if (dt.Rows.Count > 0)
            {
                return BadRequest("El proveedor ya existe");
            }

            // Insertar el nuevo proveedor
            SqlCommand cmd = new SqlCommand(@"INSERT INTO Proveedor (nombre, razonSocial, contacto, telefono, diaIngreso) 
                                      VALUES (@nombre, @razonSocial, @contacto, @telefono, @diaIngreso)", con);

            cmd.Parameters.AddWithValue("@nombre", proveedor.nombre);
            cmd.Parameters.AddWithValue("@razonSocial", proveedor.razonSocial);
            cmd.Parameters.AddWithValue("@contacto", proveedor.contacto);
            cmd.Parameters.AddWithValue("@telefono", proveedor.telefono);
            cmd.Parameters.AddWithValue("@diaIngreso", proveedor.diaIngreso);

            con.Open();
            int i = cmd.ExecuteNonQuery();
            con.Close();

            if (i > 0)
            {
                return Ok("Proveedor registrado exitosamente");
            }
            else
            {
                return StatusCode(500, "Error al registrar proveedor");
            }
        }


        [HttpDelete]
        [Route("EliminarProveedor/{id}")]
        public IActionResult EliminarProveeddor(int id)
        {
            try
            {
                using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
                string query = "DELETE FROM Proveedor WHERE idProveedor = @id";
                using SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@id", id);
                con.Open();
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                    return Ok(new { message = "Proveedor eliminado correctamente" });
                else
                    return NotFound(new { message = "Proveedor no encontrado" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"Error al eliminar proveedor: {ex.Message}" });
            }
        }

        [HttpPut]
        [Route("ActualizarProveedor/{id}")]
        public IActionResult ActualizarProveedor(int id, [FromBody] Proveedor proveedor)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
            string query = @"UPDATE Proveedor SET 
                    nombre = @nombre,
                    razonSocial = @razonSocial,
                    contacto = @contacto,
                    telefono = @telefono,
                    diaIngreso = @diaIngreso
                 WHERE idProveedor = @idProveedor";

            using SqlCommand cmd = new SqlCommand(query, con);
            cmd.Parameters.AddWithValue("@idProveedor", id);
            cmd.Parameters.AddWithValue("@nombre", proveedor.nombre);
            cmd.Parameters.AddWithValue("@razonSocial", proveedor.razonSocial);
            cmd.Parameters.AddWithValue("@contacto", proveedor.contacto);
            cmd.Parameters.AddWithValue("@telefono", proveedor.telefono);
            cmd.Parameters.AddWithValue("@diaIngreso", proveedor.diaIngreso);

            con.Open();
            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
                return Ok(new { message = "Proveedor actualizado correctamente." });
            else
                return NotFound(new { message = "Proveedor no encontrado." });
        }




    }
}
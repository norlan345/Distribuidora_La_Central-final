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
    public class AbonoController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        // 🔹 Constantes de cadenas repetidas
        private const string ConnectionName = "DefaultConnection";
        private const string EstadoActivo = "Activo";
        private const string EstadoCancelado = "Cancelado";
        private const string TablaAbono = "Abono";
        private const string TablaCredito = "Credito";

        // Parámetros SQL
        private const string ParamCodigoFactura = "@codigoFactura";

        // 🔹 Mensajes comunes
        private const string MsgNoAbonos = "No se encontraron abonos.";
        private const string MsgNoCredito = "No se encontró crédito asociado a esta factura.";
        private const string MsgCreditoNoActivo = "El crédito asociado no está activo.";
        private const string MsgMontoExcedeSaldo = "El monto del abono excede el saldo disponible.";
        private const string MsgAbonoRegistrado = "Abono registrado exitosamente.";
        private const string MsgAbonoActualizado = "Abono actualizado exitosamente.";
        private const string MsgErrorActualizar = "Error al actualizar el abono.";
        private const string MsgAbonoEliminado = "Abono eliminado exitosamente.";
        private const string MsgErrorEliminar = "Error al eliminar el abono.";
        private const string MsgNoAbonoActualizar = "No se encontró el abono a actualizar.";
        private const string MsgNoAbonoEliminar = "No se encontró el abono a eliminar.";

        public AbonoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("GetAllAbonos")]
        public string GetAbonos()
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));
            SqlDataAdapter da = new SqlDataAdapter($"SELECT * FROM {TablaAbono};", con);
            DataTable dt = new DataTable();
            da.Fill(dt);

            List<Abono> abonoList = new();
            Response response = new();

            foreach (DataRow row in dt.Rows)
            {
                abonoList.Add(new Abono
                {
                    idAbono = Convert.ToInt32(row["idAbono"]),
                    codigoFactura = Convert.ToInt32(row["codigoFactura"]),
                    montoAbono = Convert.ToDecimal(row["montoAbono"]),
                    fechaAbono = Convert.ToDateTime(row["fechaAbono"])
                });
            }

            if (abonoList.Count > 0)
                return JsonConvert.SerializeObject(abonoList);

            response.StatusCode = 100;
            response.ErrorMessage = MsgNoAbonos;
            return JsonConvert.SerializeObject(response);
        }

        [HttpPost("registrar-abono")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
        public IActionResult RegistrarAbono([FromBody] Abono abono)
        {
            using var con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));
            con.Open();
            using var transaccion = con.BeginTransaction();

            try
            {
                // 1. Verificar saldo y estado del crédito
                var cmdVerificar = new SqlCommand(
                    $@"SELECT saldoMaximo, estado 
                       FROM {TablaCredito} 
                       WHERE codigoFactura = {ParamCodigoFactura}", con, transaccion);

                cmdVerificar.Parameters.AddWithValue(ParamCodigoFactura, abono.codigoFactura);

                decimal saldoActual = 0;
                string estadoActual = string.Empty;

                using (var lector = cmdVerificar.ExecuteReader())
                {
                    if (!lector.Read())
                    {
                        transaccion.Rollback();
                        return BadRequest(MsgNoCredito);
                    }

                    saldoActual = lector.GetDecimal(0);
                    estadoActual = lector.GetString(1);
                }

                if (estadoActual != EstadoActivo)
                {
                    transaccion.Rollback();
                    return BadRequest(MsgCreditoNoActivo);
                }

                if (abono.montoAbono > saldoActual)
                {
                    transaccion.Rollback();
                    return BadRequest(MsgMontoExcedeSaldo);
                }

                // 2. Insertar abono
                var cmdInsertar = new SqlCommand(
                    $@"INSERT INTO {TablaAbono} (codigoFactura, montoAbono, fechaAbono)
                       VALUES ({ParamCodigoFactura}, @montoAbono, @fechaAbono);
                       SELECT SCOPE_IDENTITY();", con, transaccion);

                cmdInsertar.Parameters.AddWithValue(ParamCodigoFactura, abono.codigoFactura);
                cmdInsertar.Parameters.AddWithValue("@montoAbono", abono.montoAbono);
                cmdInsertar.Parameters.AddWithValue("@fechaAbono", abono.fechaAbono);

                int idAbono = Convert.ToInt32(cmdInsertar.ExecuteScalar());

                // 3. Actualizar crédito
                decimal nuevoSaldo = saldoActual - abono.montoAbono;
                string nuevoEstado = nuevoSaldo <= 0 ? EstadoCancelado : EstadoActivo;

                var cmdActualizar = new SqlCommand(
                    $@"UPDATE {TablaCredito} 
                       SET saldoMaximo = @nuevoSaldo,
                           estado = @nuevoEstado
                       WHERE codigoFactura = {ParamCodigoFactura}", con, transaccion);

                cmdActualizar.Parameters.AddWithValue("@nuevoSaldo", nuevoSaldo);
                cmdActualizar.Parameters.AddWithValue("@nuevoEstado", nuevoEstado);
                cmdActualizar.Parameters.AddWithValue(ParamCodigoFactura, abono.codigoFactura);

                cmdActualizar.ExecuteNonQuery();

                transaccion.Commit();
                return Ok(new
                {
                    success = true,
                    message = MsgAbonoRegistrado,
                    idAbono,
                    nuevoSaldo,
                    nuevoEstado
                });
            }
            catch (Exception ex)
            {
                transaccion.Rollback();
                return StatusCode(500, $"Error al registrar el abono: {ex.Message}");
            }
        }

        [HttpGet("GetByFactura/{codigoFactura}")]
        public string GetAbonosPorFactura(int codigoFactura)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));
            SqlDataAdapter da = new SqlDataAdapter(
                $"SELECT * FROM {TablaAbono} WHERE codigoFactura = {ParamCodigoFactura}", con);
            da.SelectCommand.Parameters.AddWithValue(ParamCodigoFactura, codigoFactura);

            DataTable dt = new DataTable();
            da.Fill(dt);

            List<Abono> abonoList = new();

            foreach (DataRow row in dt.Rows)
            {
                abonoList.Add(new Abono
                {
                    idAbono = Convert.ToInt32(row["idAbono"]),
                    codigoFactura = Convert.ToInt32(row["codigoFactura"]),
                    montoAbono = Convert.ToDecimal(row["montoAbono"]),
                    fechaAbono = Convert.ToDateTime(row["fechaAbono"])
                });
            }

            return JsonConvert.SerializeObject(abonoList);
        }

        [HttpPut("actualizar-abono/{idAbono}")]
        public IActionResult ActualizarAbono(int idAbono, [FromBody] Abono abono)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));

            SqlDataAdapter checkAbono = new SqlDataAdapter(
                $"SELECT * FROM {TablaAbono} WHERE idAbono = @idAbono", con);
            checkAbono.SelectCommand.Parameters.AddWithValue("@idAbono", idAbono);
            DataTable dt = new();
            checkAbono.Fill(dt);

            if (dt.Rows.Count == 0)
                return NotFound(MsgNoAbonoActualizar);

            SqlCommand cmd = new SqlCommand(
                $@"UPDATE {TablaAbono} 
                   SET codigoFactura = {ParamCodigoFactura},
                       montoAbono = @montoAbono,
                       fechaAbono = @fechaAbono
                   WHERE idAbono = @idAbono", con);

            cmd.Parameters.AddWithValue(ParamCodigoFactura, abono.codigoFactura);
            cmd.Parameters.AddWithValue("@montoAbono", abono.montoAbono);
            cmd.Parameters.AddWithValue("@fechaAbono", abono.fechaAbono);
            cmd.Parameters.AddWithValue("@idAbono", idAbono);

            con.Open();
            int i = cmd.ExecuteNonQuery();
            con.Close();

            if (i > 0)
                return Ok(MsgAbonoActualizado);
            else
                return StatusCode(500, MsgErrorActualizar);
        }

        [HttpDelete("eliminar-abono/{idAbono}")]
        public IActionResult EliminarAbono(int idAbono)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));

            SqlDataAdapter checkAbono = new SqlDataAdapter(
                $"SELECT * FROM {TablaAbono} WHERE idAbono = @idAbono", con);
            checkAbono.SelectCommand.Parameters.AddWithValue("@idAbono", idAbono);
            DataTable dt = new();
            checkAbono.Fill(dt);

            if (dt.Rows.Count == 0)
                return NotFound(MsgNoAbonoEliminar);

            SqlCommand cmd = new SqlCommand(
                $"DELETE FROM {TablaAbono} WHERE idAbono = @idAbono", con);
            cmd.Parameters.AddWithValue("@idAbono", idAbono);

            con.Open();
            int i = cmd.ExecuteNonQuery();
            con.Close();

            if (i > 0)
                return Ok(MsgAbonoEliminado);
            else
                return StatusCode(500, MsgErrorEliminar);
        }
    }
}

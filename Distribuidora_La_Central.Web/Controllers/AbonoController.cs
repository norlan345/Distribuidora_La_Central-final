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

        // Mensajes comunes
        private const string MsgNoAbonos = "No se encontraron abonos.";
        private const string MsgNoCredito = "No se encontró crédito asociado a esta factura";
        private const string MsgCreditoNoActivo = "El crédito asociado no está activo";
        private const string MsgMontoExcedeSaldo = "El monto del abono excede el saldo disponible";
        private const string MsgAbonoRegistrado = "Abono registrado exitosamente";
        private const string MsgAbonoRegistradoCorrecto = "Abono registrado correctamente";
        private const string MsgAbonoActualizado = "Abono actualizado exitosamente.";
        private const string MsgErrorActualizar = "Error al actualizar el abono.";
        private const string MsgAbonoEliminado = "Abono eliminado exitosamente.";
        private const string MsgErrorEliminar = "Error al eliminar el abono.";
        private const string MsgNoExisteCredito = "No existe crédito asociado a esta factura";
        private const string MsgCreditoNoActivo2 = "El crédito no está activo";
        private const string MsgMontoExcedeSaldo2 = "Monto excede el saldo disponible";
        private const string MsgNoAbonoActualizar = "No se encontró el abono a actualizar.";
        private const string MsgNoAbonoEliminar = "No se encontró el abono a eliminar.";

        public AbonoController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        [Route("GetAllAbonos")]
        public string GetAbonos()
        {
            SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));
            SqlDataAdapter da = new SqlDataAdapter($"SELECT * FROM {TablaAbono};", con);
            DataTable dt = new DataTable();
            da.Fill(dt);
            List<Abono> abonoList = new List<Abono>();
            Response response = new Response();

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    Abono abono = new Abono
                    {
                        idAbono = Convert.ToInt32(row["idAbono"]),
                        codigoFactura = Convert.ToInt32(row["codigoFactura"]),
                        montoAbono = Convert.ToDecimal(row["montoAbono"]),
                        fechaAbono = Convert.ToDateTime(row["fechaAbono"])
                    };
                    abonoList.Add(abono);
                }
            }

            if (abonoList.Count > 0)
            {
                return JsonConvert.SerializeObject(abonoList);
            }
            else
            {
                response.StatusCode = 100;
                response.ErrorMessage = MsgNoAbonos;
                return JsonConvert.SerializeObject(response);
            }
        }

        [HttpPost("registrar-abono")]
        public IActionResult RegistrarAbono([FromBody] Abono abono)
        {
            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName)))
            {
                con.Open();
                using (SqlTransaction transaction = con.BeginTransaction())
                {
                    try
                    {
                        SqlCommand cmdGetCredito = new SqlCommand(
                            $@"SELECT saldoMaximo, estado FROM {TablaCredito} 
                               WHERE codigoFactura = @codigoFactura",
                            con, transaction);
                        cmdGetCredito.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);

                        decimal saldoActual = 0;
                        string estadoActual = "";

                        using (var reader = cmdGetCredito.ExecuteReader())
                        {
                            if (!reader.Read())
                            {
                                transaction.Rollback();
                                return BadRequest(MsgNoCredito);
                            }

                            saldoActual = reader.GetDecimal(0);
                            estadoActual = reader.GetString(1);
                        }

                        if (estadoActual != EstadoActivo)
                        {
                            transaction.Rollback();
                            return BadRequest(MsgCreditoNoActivo);
                        }

                        if (abono.montoAbono > saldoActual)
                        {
                            transaction.Rollback();
                            return BadRequest(MsgMontoExcedeSaldo);
                        }

                        SqlCommand cmdInsertAbono = new SqlCommand(
                            $@"INSERT INTO {TablaAbono} (codigoFactura, montoAbono, fechaAbono) 
                               VALUES (@codigoFactura, @montoAbono, @fechaAbono);
                               SELECT SCOPE_IDENTITY();",
                            con, transaction);
                        cmdInsertAbono.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);
                        cmdInsertAbono.Parameters.AddWithValue("@montoAbono", abono.montoAbono);
                        cmdInsertAbono.Parameters.AddWithValue("@fechaAbono", abono.fechaAbono);

                        int idAbono = Convert.ToInt32(cmdInsertAbono.ExecuteScalar());

                        decimal nuevoSaldo = saldoActual - abono.montoAbono;
                        string nuevoEstado = nuevoSaldo <= 0 ? EstadoCancelado : EstadoActivo;

                        SqlCommand cmdUpdateCredito = new SqlCommand(
                            $@"UPDATE {TablaCredito} 
                               SET saldoMaximo = @nuevoSaldo,
                                   estado = @nuevoEstado
                               WHERE codigoFactura = @codigoFactura",
                            con, transaction);
                        cmdUpdateCredito.Parameters.AddWithValue("@nuevoSaldo", nuevoSaldo);
                        cmdUpdateCredito.Parameters.AddWithValue("@nuevoEstado", nuevoEstado);
                        cmdUpdateCredito.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);
                        cmdUpdateCredito.ExecuteNonQuery();

                        transaction.Commit();

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
                        transaction.Rollback();
                        return StatusCode(500, $"Error al registrar el abono: {ex.Message}");
                    }
                }
            }
        }

        [HttpGet("GetByFactura/{codigoFactura}")]
        public string GetAbonosPorFactura(int codigoFactura)
        {
            SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));
            SqlDataAdapter da = new SqlDataAdapter(
                $"SELECT * FROM {TablaAbono} WHERE codigoFactura = @codigoFactura",
                con);
            da.SelectCommand.Parameters.AddWithValue("@codigoFactura", codigoFactura);

            DataTable dt = new DataTable();
            da.Fill(dt);

            List<Abono> abonoList = new List<Abono>();

            if (dt.Rows.Count > 0)
            {
                foreach (DataRow row in dt.Rows)
                {
                    Abono abono = new Abono
                    {
                        idAbono = Convert.ToInt32(row["idAbono"]),
                        codigoFactura = Convert.ToInt32(row["codigoFactura"]),
                        montoAbono = Convert.ToDecimal(row["montoAbono"]),
                        fechaAbono = Convert.ToDateTime(row["fechaAbono"])
                    };
                    abonoList.Add(abono);
                }
            }

            return JsonConvert.SerializeObject(abonoList);
        }

        [HttpPost("Registrar")]
        public IActionResult RegistrarNuevoAbono([FromBody] Abono abono)
        {
            using (SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName)))
            {
                con.Open();
                using (SqlTransaction transaccion = con.BeginTransaction())
                {
                    try
                    {
                        var cmdVerificarCredito = new SqlCommand(
                            $@"SELECT saldoMaximo, estado FROM {TablaCredito} 
                               WHERE codigoFactura = @codigoFactura",
                            con, transaccion);
                        cmdVerificarCredito.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);

                        using (var lector = cmdVerificarCredito.ExecuteReader())
                        {
                            if (!lector.Read())
                            {
                                transaccion.Rollback();
                                return BadRequest(MsgNoExisteCredito);
                            }

                            decimal saldoActual = lector.GetDecimal(0);
                            string estadoActual = lector.GetString(1);
                            lector.Close();

                            if (estadoActual != EstadoActivo)
                                return BadRequest(MsgCreditoNoActivo2);

                            if (abono.montoAbono > saldoActual)
                                return BadRequest(MsgMontoExcedeSaldo2);

                            var cmdRegistrarAbono = new SqlCommand(
                                $@"INSERT INTO {TablaAbono} (codigoFactura, montoAbono, fechaAbono) 
                                   VALUES (@codigoFactura, @montoAbono, @fechaAbono);
                                   SELECT SCOPE_IDENTITY();",
                                con, transaccion);

                            cmdRegistrarAbono.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);
                            cmdRegistrarAbono.Parameters.AddWithValue("@montoAbono", abono.montoAbono);
                            cmdRegistrarAbono.Parameters.AddWithValue("@fechaAbono", abono.fechaAbono);

                            int idAbono = Convert.ToInt32(cmdRegistrarAbono.ExecuteScalar());

                            decimal nuevoSaldo = saldoActual - abono.montoAbono;
                            string nuevoEstado = nuevoSaldo <= 0 ? EstadoCancelado : EstadoActivo;

                            var cmdActualizarCredito = new SqlCommand(
                                $@"UPDATE {TablaCredito} 
                                   SET saldoMaximo = @nuevoSaldo,
                                       estado = @nuevoEstado
                                   WHERE codigoFactura = @codigoFactura",
                                con, transaccion);

                            cmdActualizarCredito.Parameters.AddWithValue("@nuevoSaldo", nuevoSaldo);
                            cmdActualizarCredito.Parameters.AddWithValue("@nuevoEstado", nuevoEstado);
                            cmdActualizarCredito.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);
                            cmdActualizarCredito.ExecuteNonQuery();

                            transaccion.Commit();

                            return Ok(new
                            {
                                Exito = true,
                                Mensaje = MsgAbonoRegistradoCorrecto,
                                IdAbono = idAbono,
                                NuevoSaldo = nuevoSaldo,
                                EstadoActual = nuevoEstado
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        transaccion.Rollback();
                        return StatusCode(500, $"Error al procesar el abono: {ex.Message}");
                    }
                }
            }
        }

        [HttpPut("actualizar-abono/{idAbono}")]
        public IActionResult ActualizarAbono(int idAbono, [FromBody] Abono abono)
        {
            using SqlConnection con = new SqlConnection(_configuration.GetConnectionString(ConnectionName));

            SqlDataAdapter checkAbono = new SqlDataAdapter($"SELECT * FROM {TablaAbono} WHERE idAbono = @idAbono", con);
            checkAbono.SelectCommand.Parameters.AddWithValue("@idAbono", idAbono);
            DataTable dt = new DataTable();
            checkAbono.Fill(dt);

            if (dt.Rows.Count == 0)
                return NotFound(MsgNoAbonoActualizar);

            SqlCommand cmd = new SqlCommand($@"UPDATE {TablaAbono} 
                                               SET codigoFactura = @codigoFactura,
                                                   montoAbono = @montoAbono,
                                                   fechaAbono = @fechaAbono
                                             WHERE idAbono = @idAbono", con);

            cmd.Parameters.AddWithValue("@codigoFactura", abono.codigoFactura);
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

            SqlDataAdapter checkAbono = new SqlDataAdapter($"SELECT * FROM {TablaAbono} WHERE idAbono = @idAbono", con);
            checkAbono.SelectCommand.Parameters.AddWithValue("@idAbono", idAbono);
            DataTable dt = new DataTable();
            checkAbono.Fill(dt);

            if (dt.Rows.Count == 0)
                return NotFound(MsgNoAbonoEliminar);

            SqlCommand cmd = new SqlCommand($"DELETE FROM {TablaAbono} WHERE idAbono = @idAbono", con);
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

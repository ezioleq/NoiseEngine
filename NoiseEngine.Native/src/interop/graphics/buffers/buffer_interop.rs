use crate::{
    graphics::buffers::buffer::GraphicsBuffer,
    interop::prelude::{InteropReadOnlySpan, InteropResult, InteropSpan}
};

#[no_mangle]
extern "C" fn graphics_buffers_buffer_interop_destroy(_handle: Box<Box<dyn GraphicsBuffer>>) {
}

#[no_mangle]
extern "C" fn graphics_buffers_buffer_interop_host_read(
    buffer: &Box<dyn GraphicsBuffer>, destination_buffer: InteropSpan<u8>, start: u64
) -> InteropResult<()> {
    buffer.host_read(destination_buffer.into(), start)
}

#[no_mangle]
extern "C" fn graphics_buffers_buffer_interop_host_write(
    buffer: &Box<dyn GraphicsBuffer>, data: InteropReadOnlySpan<u8>, start: u64
) -> InteropResult<()> {
    buffer.host_write(data.into(), start)
}